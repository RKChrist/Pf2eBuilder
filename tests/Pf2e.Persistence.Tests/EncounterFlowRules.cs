using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The encounter through a real database and real handlers. The timing rules themselves are
/// asserted against the pure functions in the behaviour suite; this is about the parts that
/// only exist once the state is stored: the marker surviving a reordering, the mode following
/// initiative, and the round counting when the order wraps.
/// </summary>
public class EncounterFlowRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    const string Ogre = "creature-126";
    const string Wolf = "creature-501";

    RecordingBroadcaster Broadcaster { get; } = new();

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    async Task<CreatedCampaignView> NewCampaign()
    {
        await using var db = database.NewContext();
        return await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
    }

    async Task<CharacterSheetView> Import(string code, string name)
    {
        var payload = JsonNode.Parse(Fixture("gnibbo.json"))!;
        payload["build"]!["name"] = name;

        await using var db = database.NewContext();
        return await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(code, payload.ToJsonString()), default);
    }

    async Task<CampaignView> AddMonster(
        CreatedCampaignView campaign, string ruleId, string name, int? initiative = null)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Broadcaster)
            .Handle(new AddCombatant(campaign.Code, campaign.DmKey, ruleId, null, name, initiative), default);
    }

    async Task<CampaignView> AddPlayer(CreatedCampaignView campaign, Guid characterId)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Broadcaster)
            .Handle(new AddCombatant(campaign.Code, campaign.DmKey, null, characterId, null), default);
    }

    async Task<CampaignView> Roll(CreatedCampaignView campaign, params InitiativeRoll[] rolls)
    {
        await using var db = database.NewContext();
        return await new RollInitiativeHandler(db, Broadcaster)
            .Handle(new RollInitiative(campaign.Code, campaign.DmKey, rolls), default);
    }

    async Task<CampaignView> Next(CreatedCampaignView campaign)
    {
        await using var db = database.NewContext();
        return await new NextTurnHandler(db, Broadcaster)
            .Handle(new NextTurn(campaign.Code, campaign.DmKey), default);
    }

    static Guid MonsterNamed(CampaignView view, string name) =>
        view.Encounter!.Combatants.Single(c => c.Name == name).Id;

    // Rolling initiative is what enters Encounter mode, and ending the fight returns to
    // Exploration. That is the document's state diagram and it is the DM's to drive.
    [Fact]
    public async Task RollingInitiativeEntersEncounterModeAndEndingItReturnsToExploration()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgre = await AddMonster(campaign, Ogre, "Ogre boss");

        Assert.Equal("Exploration", withOgre.Mode);

        var started = await Roll(campaign,
            new InitiativeRoll(bard.Id, 21),
            new InitiativeRoll(MonsterNamed(withOgre, "Ogre boss"), 21));

        Assert.Equal("Encounter", started.Mode);
        Assert.Equal(1, started.Encounter!.Round);

        // The tie rule decides who starts, so the ogre holds the marker on round one.
        Assert.Equal("Ogre boss", started.Encounter.Combatants.Single(c => c.IsCurrentTurn).Name);

        await using var db = database.NewContext();
        var ended = await new EndEncounterHandler(db, Broadcaster)
            .Handle(new EndEncounter(campaign.Code, campaign.DmKey), default);

        Assert.Equal("Exploration", ended.Mode);
        Assert.Null(ended.Encounter);
    }

    [Fact]
    public async Task TheRoundCountsUpWhenTheOrderWraps()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgre = await AddMonster(campaign, Ogre, "Ogre boss");
        var ogre = MonsterNamed(withOgre, "Ogre boss");

        await Roll(campaign, new InitiativeRoll(bard.Id, 21), new InitiativeRoll(ogre, 23));

        var toBard = await Next(campaign);
        Assert.Equal(1, toBard.Encounter!.Round);
        Assert.Equal(bard.Id, toBard.Encounter.CurrentCombatantId);

        var wrapped = await Next(campaign);
        Assert.Equal(2, wrapped.Encounter!.Round);
        Assert.Equal(ogre, wrapped.Encounter.CurrentCombatantId);
    }

    // The marker is stored as a combatant id. A reinforcement whose initiative puts it above the
    // creature holding the marker changes every index below it, and an index-based marker would
    // move the turn onto whoever is now standing at that position.
    [Fact]
    public async Task AReinforcementArrivingMidFightLeavesTheTurnOnTheSameCreature()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        var fighter = await Import(campaign.Code, "Rune");
        await AddPlayer(campaign, bard.Id);
        await AddPlayer(campaign, fighter.Id);
        var withOgre = await AddMonster(campaign, Ogre, "Ogre boss");
        var ogre = MonsterNamed(withOgre, "Ogre boss");

        var started = await Roll(campaign,
            new InitiativeRoll(ogre, 23),
            new InitiativeRoll(bard.Id, 21),
            new InitiativeRoll(fighter.Id, 12));

        var onBard = await Next(campaign);
        Assert.Equal(bard.Id, onBard.Encounter!.CurrentCombatantId);
        Assert.Equal(1, IndexOf(onBard, bard.Id));

        // A wolf on 22 lands between the ogre and the bard, which moves the bard from index 1
        // to index 2. An index-based marker would now be standing on the wolf.
        var withWolf = await AddMonster(campaign, Wolf, "Wolf", initiative: 22);

        Assert.Equal(2, IndexOf(withWolf, bard.Id));
        Assert.Equal("Wolf", withWolf.Encounter!.Combatants[1].Name);
        Assert.Equal(bard.Id, withWolf.Encounter.CurrentCombatantId);
        Assert.Equal(1, withWolf.Encounter.Round);
        Assert.Equal(started.Encounter!.Combatants.Count + 1, withWolf.Encounter.Combatants.Count);

        // And the next turn goes to whoever really follows the bard, not to the wolf.
        var after = await Next(campaign);
        Assert.Equal(fighter.Id, after.Encounter!.CurrentCombatantId);
    }

    static int IndexOf(CampaignView view, Guid id) =>
        view.Encounter!.Combatants.Select(c => c.Id).ToList().IndexOf(id);
}
