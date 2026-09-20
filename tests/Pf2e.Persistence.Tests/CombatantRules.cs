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

    async Task<CampaignView> AddMonster(CreatedCampaignView campaign, string? name = null)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Stack, Broadcaster)
            .Handle(new AddCombatant(campaign.Code, campaign.DmKey, Ogre, null, name), default);
    }

    static IReadOnlyList<string> Names(CampaignView view) =>
        [.. view.Encounter!.Combatants.Select(c => c.Name)];

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
    // One of a creature is that creature. Two are a problem: they take damage separately and
    // the GM says "the one on the left", which an order printing the same name twice cannot
    // answer. The name comes off the record rather than being written here, because which
    // creature the fixture id points at is not what this is about.
    [Fact]
    public async Task TheSecondOfACreatureNumbersItselfAndRenamesTheFirst()
    {
        var campaign = await NewCampaign();

        var alone = await AddMonster(campaign);
        var beast = Assert.Single(Names(alone));

        var pair = await AddMonster(campaign);
        Assert.Equal([$"{beast} 1", $"{beast} 2"], [.. Names(pair).Order()]);

        var three = await AddMonster(campaign);
        Assert.Contains($"{beast} 3", Names(three));
    }

    // What numbering is for is that no two rows in the order ever read the same. It is not
    // that a number is retired: once the second one is dead and gone there is no second one,
    // and a new arrival taking the number is unambiguous at every moment anybody is looking.
    [Fact]
    public async Task NoTwoCreaturesInTheOrderEverShareAName()
    {
        var campaign = await NewCampaign();
        var alone = await AddMonster(campaign);
        var beast = Assert.Single(Names(alone));
        var pair = await AddMonster(campaign);

        var second = pair.Encounter!.Combatants.Single(c => c.Name == $"{beast} 2");
        await Remove(campaign, second.Id);

        var replaced = await AddMonster(campaign);
        var names = Names(replaced);

        Assert.Equal(2, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    // A GM who names one keeps the name they gave it. Numbering is for the ones nobody named.
    [Fact]
    public async Task ANamedMonsterKeepsItsName()
    {
        var campaign = await NewCampaign();
        await AddMonster(campaign, "Grukk the Elder");
        var pair = await AddMonster(campaign, "Grukk the Younger");

        Assert.Equal(["Grukk the Elder", "Grukk the Younger"], [.. Names(pair).Order()]);
    }
// The edit screen collects a name, a level, six attributes, five proficiencies and the
    // armour and hit point inputs. It used to hand those to the writer that sets every field
    // on a character, so correcting a level erased the weapons, skills, feats and spells the
    // import had brought in, and the only sign was the attacks going missing off the fight
    // page. Nothing an edit cannot express is reachable from an edit.
    [Fact]
    public async Task CorrectingALevelKeepsEverythingTheImportBroughtIn()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, "Zuz");

        Assert.NotEmpty(bard.Attacks);
        Assert.NotEmpty(bard.Skills);
        Assert.NotEmpty(bard.Feats);
        Assert.NotEmpty(bard.Spells);
        Assert.NotNull(bard.SpellAttack);

        await using var db = database.NewContext();
        var edited = await new EditCharacterHandler(db, Broadcaster).Handle(
            new EditCharacter(campaign.Code, bard.Id, bard.Build with { Level = 8 }),
            default);

        Assert.Equal(8, edited.Level);
        Assert.Equal(bard.Attacks.Select(a => a.Name), edited.Attacks.Select(a => a.Name));
        Assert.Equal(bard.Skills.Select(a => a.Name), edited.Skills.Select(a => a.Name));
        Assert.Equal(bard.Feats.Select(a => a.Name), edited.Feats.Select(a => a.Name));
        Assert.Equal(bard.Spells.Select(a => a.Name), edited.Spells.Select(a => a.Name));
        Assert.NotNull(edited.SpellAttack);
    }
}
