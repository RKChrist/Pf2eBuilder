using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The two corrections a fight needs and could not make: one initiative, and one creature
/// leaving. Both existed only as the whole-fight commands next to them, which is what made them
/// worth adding: rolling again to fix a misheard number re-rolled everybody, and ending the
/// encounter to remove one ogre ended it for the party too.
/// </summary>
public class CombatantRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    const string Ogre = "creature-126";

    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Stack { get; } = new();

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

    async Task<CampaignView> AddMonster(CreatedCampaignView campaign, string name)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Stack, Broadcaster)
            .Handle(new AddCombatant(campaign.Code, campaign.DmKey, Ogre, null, name), default);
    }

    async Task<CampaignView> AddPlayer(CreatedCampaignView campaign, Guid characterId)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Stack, Broadcaster)
            .Handle(new AddCombatant(campaign.Code, campaign.DmKey, null, characterId, null), default);
    }

    async Task<CampaignView> Roll(CreatedCampaignView campaign, params InitiativeRoll[] rolls)
    {
        await using var db = database.NewContext();
        return await new RollInitiativeHandler(db, Stack, Broadcaster)
            .Handle(new RollInitiative(campaign.Code, campaign.DmKey, rolls), default);
    }

    // asPlayer rather than a nullable key, because a null key defaulting to the DM key is how a
    // role test passes while asserting nothing.
    async Task<CampaignView> SetInitiative(
        CreatedCampaignView campaign, Guid combatant, int initiative, bool asPlayer = false)
    {
        await using var db = database.NewContext();
        return await new SetInitiativeHandler(db, Stack, Broadcaster)
            .Handle(
                new SetInitiative(campaign.Code, asPlayer ? null : campaign.DmKey, combatant, initiative),
                default);
    }

    async Task<CampaignView> Remove(
        CreatedCampaignView campaign, Guid combatant, bool asPlayer = false)
    {
        await using var db = database.NewContext();
        return await new RemoveCombatantHandler(db, Stack, Broadcaster)
            .Handle(
                new RemoveCombatant(campaign.Code, asPlayer ? null : campaign.DmKey, combatant),
                default);
    }

    static Guid Named(CampaignView view, string name) =>
        view.Encounter!.Combatants.Single(c => c.Name == name).Id;

    static int Initiative(CampaignView view, Guid id) =>
        view.Encounter!.Combatants.Single(c => c.Id == id).Initiative;

    // A player says twenty-one and meant twelve. Correcting it must not touch anybody else's
    // number, which is exactly what rolling again would do.
    [Fact]
    public async Task CorrectingOneInitiativeLeavesEveryOtherNumberAlone()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgre = await AddMonster(campaign, "Ogre boss");
        var ogre = Named(withOgre, "Ogre boss");

        await Roll(campaign, new InitiativeRoll(bard.Id, 21), new InitiativeRoll(ogre, 14));

        var fixedUp = await SetInitiative(campaign, bard.Id, 12);

        Assert.Equal(12, Initiative(fixedUp, bard.Id));
        Assert.Equal(14, Initiative(fixedUp, ogre));
    }

    // The round and the marker belong to the fight, not to the number. Correcting an initiative
    // is a correction, so round three stays round three and whoever was acting still is.
    [Fact]
    public async Task CorrectingAnInitiativeDoesNotRestartTheRoundOrMoveTheTurn()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgre = await AddMonster(campaign, "Ogre boss");
        var ogre = Named(withOgre, "Ogre boss");

        var started = await Roll(campaign,
            new InitiativeRoll(bard.Id, 21), new InitiativeRoll(ogre, 14));

        Assert.Equal(bard.Id, started.Encounter!.CurrentCombatantId);

        var fixedUp = await SetInitiative(campaign, bard.Id, 2);

        Assert.Equal(1, fixedUp.Encounter!.Round);
        Assert.Equal(bard.Id, fixedUp.Encounter.CurrentCombatantId);
    }

    [Fact]
    public async Task OnlyTheDmSetsAnInitiative()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        await Roll(campaign, new InitiativeRoll(bard.Id, 21));

        await Assert.ThrowsAsync<NotTheDmException>(
            () => SetInitiative(campaign, bard.Id, 3, asPlayer: true));
    }

    // Taking a creature out of the fight is not ending the fight, which was the only way to do
    // it before and took the whole encounter with it.
    [Fact]
    public async Task ACombatantLeavesWithoutEndingTheFight()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgres = await AddMonster(campaign, "Ogre boss");
        await AddMonster(campaign, "Ogre two");
        var wrong = Named(withOgres, "Ogre boss");

        await Roll(campaign, new InitiativeRoll(bard.Id, 21), new InitiativeRoll(wrong, 14));

        var without = await Remove(campaign, wrong);

        Assert.NotNull(without.Encounter);
        Assert.DoesNotContain(without.Encounter!.Combatants, c => c.Id == wrong);
        Assert.Contains(without.Encounter.Combatants, c => c.Name == "Ogre two");
        Assert.Contains(without.Encounter.Combatants, c => c.Id == bard.Id);
    }

    // The marker points at a combatant, so the creature it points at going away must move it
    // first or the next turn has nowhere to start from.
    [Fact]
    public async Task RemovingTheCreatureWhoseTurnItIsMovesTheTurnOn()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");
        await AddPlayer(campaign, bard.Id);
        var withOgre = await AddMonster(campaign, "Ogre boss");
        var ogre = Named(withOgre, "Ogre boss");

        var started = await Roll(campaign,
            new InitiativeRoll(ogre, 23), new InitiativeRoll(bard.Id, 21));

        Assert.Equal(ogre, started.Encounter!.CurrentCombatantId);

        var without = await Remove(campaign, ogre);

        Assert.Equal(bard.Id, without.Encounter!.CurrentCombatantId);
    }

    // The last creature out leaves a fight with nobody in it rather than a marker pointing at a
    // row that is not there.
    [Fact]
    public async Task RemovingTheLastCombatantLeavesNobodyHoldingTheTurn()
    {
        var campaign = await NewCampaign();
        var withOgre = await AddMonster(campaign, "Ogre alone");
        var ogre = Named(withOgre, "Ogre alone");
        await Roll(campaign, new InitiativeRoll(ogre, 15));

        var empty = await Remove(campaign, ogre);

        Assert.Empty(empty.Encounter!.Combatants);
        Assert.Null(empty.Encounter.CurrentCombatantId);
    }

    [Fact]
    public async Task OnlyTheDmRemovesACombatant()
    {
        var campaign = await NewCampaign();
        var withOgre = await AddMonster(campaign, "Ogre boss");

        await Assert.ThrowsAsync<NotTheDmException>(
            () => Remove(campaign, Named(withOgre, "Ogre boss"), asPlayer: true));
    }
}
