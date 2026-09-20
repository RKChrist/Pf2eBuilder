using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// What the party is doing between fights, and the part of it that reaches the fight.
/// <para>Three of the nine activities have a consequence this engine carries: Avoid Notice rolls
/// a different skill for initiative, Scout hands every ally a bonus, and Defend leaves a shield
/// raised that the engine cannot value and so states as a reminder. The other six are the
/// table's to play out, and these tests are what says so.</para>
/// </summary>
public class ExplorationRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    const string Ogre = "creature-126";

    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Undo { get; } = new();

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
        return await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(code, payload.ToJsonString()), default);
    }

    async Task<CampaignView> Doing(string code, Guid character, string? activity, string? dmKey = null)
    {
        await using var db = database.NewContext();
        return await new SetExplorationActivityHandler(db, Undo, Broadcaster)
            .Handle(new SetExplorationActivity(code, dmKey, character, activity), default);
    }

    async Task<CampaignView> AddMonster(CreatedCampaignView campaign, string ruleId, string? name = null)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Undo, Broadcaster).Handle(
            new AddCombatant(campaign.Code, campaign.DmKey, ruleId, null, name, null), default);
    }

    async Task<CampaignView> AddCharacter(CreatedCampaignView campaign, Guid characterId)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Undo, Broadcaster).Handle(
            new AddCombatant(campaign.Code, campaign.DmKey, null, characterId, null, null), default);
    }

    async Task<CampaignView> Roll(CreatedCampaignView campaign)
    {
        await using var db = database.NewContext();
        return await new RollInitiativeHandler(db, Undo, Broadcaster)
            .Handle(new RollInitiative(campaign.Code, campaign.DmKey, []), default);
    }

    [Fact]
    public async Task AnActivityIsRememberedAndClearedByName()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var scouting = await Doing(campaign.Code, gnibbo.Id, "scout");
        Assert.Equal("scout", scouting.Characters.Single().ExplorationActivity);

        var idle = await Doing(campaign.Code, gnibbo.Id, null);
        Assert.Null(idle.Characters.Single().ExplorationActivity);
    }

    [Fact]
    public async Task SettingAnActivityLeavesTheDmStillTheDm()
    {
        // This is not a permission: anyone at the table may set an activity. It is the
        // projection reading the key to decide which role it is answering. Sending null demoted
        // the DM's own screen the moment they used it, and the mode switch and the encounter
        // controls vanished under them.
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var asDm = await Doing(campaign.Code, gnibbo.Id, "scout", campaign.DmKey);
        Assert.Equal("Dm", asDm.Role);

        var asPlayer = await Doing(campaign.Code, gnibbo.Id, "search");
        Assert.Equal("Player", asPlayer.Role);
        Assert.Equal("search", asPlayer.Characters.Single().ExplorationActivity);
    }

    [Fact]
    public async Task AnActivityNobodyPrintedIsRefused()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var result = new SetExplorationActivityValidator()
            .Validate(new SetExplorationActivity(campaign.Code, null, gnibbo.Id, "sunbathing"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task AnActivitySurvivesALevelUpBecauseItIsNotBuildState()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Doing(campaign.Code, gnibbo.Id, "search");

        // The same path a re-import takes, which is the path that replaces the build.
        await using (var db = database.NewContext())
        {
            var levelled = JsonNode.Parse(Fixture("gnibbo.json"))!;
            levelled["build"]!["level"] = 8;
            await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
                .Handle(new ImportCharacter(campaign.Code, levelled.ToJsonString()), default);
        }

        await using var read = database.NewContext();
        var after = await new GetCampaignHandler(read)
            .Handle(new GetCampaign(campaign.Code, campaign.DmKey), default);

        var character = after.Characters.Single();
        Assert.Equal(8, character.Level);
        Assert.Equal("search", character.ExplorationActivity);
    }

    /// <summary>The wiring, not the arithmetic: that an activity set through the API is the one
    /// the roll reads. What it does to the number is asserted without a die in
    /// Pf2e.Behaviour.Tests.InitiativeRules.</summary>
    [Fact]
    public async Task TheActivityAPlayerChoseIsTheOneTheRollReads()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Doing(campaign.Code, gnibbo.Id, "avoid-notice");
        await AddCharacter(campaign, gnibbo.Id);

        var rolled = await Roll(campaign);
        var initiative = rolled.Encounter!.Combatants.Single(c => c.Id == gnibbo.Id).Initiative;

        // Stealth is +14 on this sheet, so a d20 on top of it lands between 15 and 34 and can
        // land nowhere else. Perception would be +12, which overlaps, so this is a guard against
        // a zero or a dropped modifier rather than a proof of which statistic was used.
        Assert.InRange(initiative, 15, 34);
        Assert.Equal("avoid-notice", rolled.Characters.Single().ExplorationActivity);
    }

    [Fact]
    public async Task DefendIsAReminderRatherThanAShieldTheEngineInvents()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Doing(campaign.Code, gnibbo.Id, "defend");
        await AddCharacter(campaign, gnibbo.Id);

        var before = gnibbo.ArmorClass.Total;
        var rolled = await Roll(campaign);

        // The shield is real and its bonus is the shield's own, which nothing here knows. So the
        // number does not move and the DM is told instead.
        Assert.Equal(before, rolled.Characters.Single().ArmorClass.Total);
        Assert.Contains(rolled.Encounter!.Reminders, reminder => reminder.Contains("Defending"));
    }

    [Fact]
    public async Task AnActivityWithNoMechanicalConsequenceChangesNoNumber()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        var before = gnibbo.ArmorClass.Total;

        var searching = await Doing(campaign.Code, gnibbo.Id, "search");

        var character = searching.Characters.Single();
        Assert.Equal(before, character.ArmorClass.Total);
        Assert.Equal(gnibbo.Perception.Total, character.Perception.Total);
    }

    [Fact]
    public void EveryActivityNamesItselfAndSaysWhatItMeans()
    {
        Assert.Equal(9, ExplorationActivities.All.Length);
        Assert.All(ExplorationActivities.All, activity =>
        {
            Assert.False(string.IsNullOrWhiteSpace(activity.Name));
            Assert.True(activity.Consequence.Length > 20, activity.Name);
            Assert.EndsWith(".", activity.Consequence);
        });

        // Exactly one rolls a skill, and it names the skill the engine can find.
        var rolled = Assert.Single(
            ExplorationActivities.All.Where(a => a.Effect is InitiativeEffect.RolledWith));
        Assert.NotNull(rolled.Skill);
        Assert.NotNull(Skills.For(rolled.Skill!));
    }
}
