using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Rules;
using Pf2e.Application.Features.Tracker;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain.Rules;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>Records what a table would have been told, so a test can assert that it was.</summary>
sealed class RecordingBroadcaster : ITableBroadcaster
{
    public List<(string Code, CharacterSheetView Sheet)> Sent { get; } = [];

    public Task CharacterChangedAsync(string tableCode, CharacterSheetView sheet, CancellationToken ct)
    {
        Sent.Add((tableCode, sheet));
        return Task.CompletedTask;
    }
}

/// <summary>
/// The importer needs the seeded armour records to know what studded leather is worth, so it is
/// exercised here against a real database rather than against a substitute that would only
/// prove the test's own idea of the ruleset.
/// </summary>
public class TrackerRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    RecordingBroadcaster Broadcaster { get; } = new();

    async Task<CharacterSheetView> Import(string code, string pathbuilder)
    {
        await using var db = database.NewContext();
        return await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(code, pathbuilder), default);
    }

    async Task<CharacterSheetView?> Damage(string code, Guid characterId, int delta)
    {
        await using var db = database.NewContext();
        return await new ChangeHitPointsHandler(db, Broadcaster)
            .Handle(new ChangeHitPoints(code, characterId, delta), default);
    }

    async Task<CharacterSheetView?> Set(string code, Guid characterId, Guid slot, EffectSpec? effect)
    {
        await using var db = database.NewContext();
        return await new SetEffectHandler(db, Broadcaster)
            .Handle(new SetEffect(code, characterId, slot, effect), default);
    }

    async Task<TableView> Read(string code)
    {
        await using var db = database.NewContext();
        return await new GetTableHandler(db).Handle(new GetTable(code), default);
    }

    static string Shape(CharacterSheetView sheet) =>
        System.Text.Json.JsonSerializer.Serialize(sheet with { Id = Guid.Empty });

    static EffectSpec Custom(string name, string type, int value, string stat) =>
        new(name, "Custom", null, 0, null,
            [new EffectModifierView(type, value, [new SelectorSpecView("Exactly", stat, null, null)])]);

    [Fact]
    public async Task ImportingTheGoblinBardGivesSeventySixHitPointsAndTwentyFiveArmorClass()
    {
        var sheet = await Import("GNIB01", Fixture("gnibbo.json"));

        Assert.Equal("Gnibbo", sheet.Name);
        Assert.Equal(76, sheet.MaxHitPoints);
        Assert.Equal(76, sheet.CurrentHitPoints);
        Assert.Equal(25, sheet.ArmorClass.Total);

        // The export states an acTotal of 24 and it is never read, because recomputing the
        // number from the build and the seeded ruleset is the point of this product.
        Assert.Contains("\"acTotal\": 24", Fixture("gnibbo.json"));

        // The armour's own bonus is one +3 item modifier, the seeded ac of 2 plus one potency
        // rune, rather than a +2 and a +1 the stacking rule would refuse to combine.
        var armor = Assert.Single(sheet.ArmorClass.Applied);
        Assert.Equal("Studded Leather Armor", armor.Source);
        Assert.Equal("Item", armor.Type);
        Assert.Equal(3, armor.Value);
        Assert.Equal(22, sheet.ArmorClass.Base);

        Assert.Equal(("GNIB01", sheet), Assert.Single(Broadcaster.Sent));
    }

    [Fact]
    public async Task ReImportingReplacesTheBuildAndLeavesTheSessionWhereItWas()
    {
        var first = await Import("GNIB02", Fixture("gnibbo.json"));
        await Damage("GNIB02", first.Id, -30);

        var levelled = JsonNode.Parse(Fixture("gnibbo.json"))!;
        levelled["build"]!["level"] = 8;
        var second = await Import("GNIB02", levelled.ToJsonString());

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(8, second.Level);
        Assert.Equal(86, second.MaxHitPoints);
        Assert.Equal(46, second.CurrentHitPoints);
        Assert.Equal(26, second.ArmorClass.Total);
    }

    [Fact]
    public async Task TwoHitPointChangesInSequenceSumOnTheStoredValue()
    {
        var character = await Import("GNIB03", Fixture("gnibbo.json"));

        await Damage("GNIB03", character.Id, -10);
        var after = await Damage("GNIB03", character.Id, -7);

        Assert.Equal(59, after!.CurrentHitPoints);
        Assert.Equal(59, Assert.Single((await Read("GNIB03")).Characters).CurrentHitPoints);
    }

    [Fact]
    public async Task AnAbsentCharacterIsNullRatherThanAFault()
    {
        await Import("GNIB04", Fixture("gnibbo.json"));

        Assert.Null(await Damage("GNIB04", Guid.NewGuid(), -1));
        Assert.Null(await Set("GNIB04", Guid.NewGuid(), Guid.NewGuid(), null));
    }

    [Fact]
    public async Task EmptyingASlotRemovesItsEffectAndEmptyingAnEmptySlotStillSucceeds()
    {
        var character = await Import("GNIB05", Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();

        var applied = await Set("GNIB05", character.Id, slot,
            new EffectSpec("Clumsy", "Seeded", "clumsy", 2, "1 minute", []));
        Assert.Equal(23, applied!.ArmorClass.Total);

        var removed = await Set("GNIB05", character.Id, slot, null);
        Assert.Empty(removed!.Effects);
        Assert.Equal(25, removed.ArmorClass.Total);

        var again = await Set("GNIB05", character.Id, Guid.NewGuid(), null);
        Assert.Empty(again!.Effects);
    }

    [Fact]
    public async Task TheSameEffectSentTwiceLeavesOneEffect()
    {
        var character = await Import("GNIB06", Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();
        var spec = new EffectSpec("Clumsy", "Seeded", "clumsy", 2, null, []);

        await Set("GNIB06", character.Id, slot, spec);
        var second = await Set("GNIB06", character.Id, slot, spec);

        Assert.Single(second!.Effects);
        Assert.Equal(23, second.ArmorClass.Total);
        Assert.Single(Assert.Single((await Read("GNIB06")).Characters).Effects);
    }

    [Fact]
    public async Task TwoCustomEffectsInDifferentSlotsBothPersistAndBothReachTheSheet()
    {
        var character = await Import("GNIB07", Fixture("gnibbo.json"));

        await Set("GNIB07", character.Id, Guid.NewGuid(), Custom("Heroism", "Status", 2, "Will"));
        await Set("GNIB07", character.Id, Guid.NewGuid(), Custom("Resolve", "Circumstance", 1, "Will"));

        var stored = Assert.Single((await Read("GNIB07")).Characters);

        Assert.Equal(2, stored.Effects.Count);
        Assert.Equal(15, stored.Will.Total);
        Assert.Equal(["Heroism", "Resolve"], stored.Will.Applied.Select(m => m.Source).Order());
        Assert.All(stored.Effects, effect => Assert.Equal("Custom", effect.Kind));
        Assert.Contains(stored.Effects, effect => effect.Modifiers.Single().Applies.Single().Stat == "Will");
    }

    [Fact]
    public async Task TwoCharactersOnOneTableBothPersist()
    {
        await Import("GNIB08", Fixture("gnibbo.json"));

        var second = JsonNode.Parse(Fixture("gnibbo.json"))!;
        second["build"]!["name"] = "Tarrow";
        await Import("GNIB08", second.ToJsonString());

        var table = await Read("GNIB08");

        Assert.Equal(["Gnibbo", "Tarrow"], table.Characters.Select(c => c.Name));
        Assert.All(table.Characters, character => Assert.Equal(76, character.MaxHitPoints));
    }

    [Fact]
    public async Task AnExportMissingTheSixDriftingKeysImportsIdentically()
    {
        var full = await Import("GNIB09", Fixture("gnibbo.json"));
        var vintage = await Import("GNIB10", Fixture("gnibbo-vintage.json"));

        foreach (var key in new[] { "dualClass", "xp", "sizeName", "rituals", "resistances", "inventorMods" })
        {
            Assert.Contains($"\"{key}\"", Fixture("gnibbo.json"));
            Assert.DoesNotContain($"\"{key}\"", Fixture("gnibbo-vintage.json"));
        }

        // Compared as JSON rather than by record equality, because a record's equality over a
        // list is by reference and would pass for any two sheets at all.
        Assert.Equal(Shape(full), Shape(vintage));
    }

    [Fact]
    public async Task APayloadThatIsNotAnExportFailsWithASentenceAPlayerCanRead()
    {
        var notJson = await Assert.ThrowsAsync<PathbuilderFormatException>(
            () => Import("GNIB11", "paste your character here"));
        var noName = await Assert.ThrowsAsync<PathbuilderFormatException>(
            () => Import("GNIB11", "{\"success\":true,\"build\":{\"level\":7}}"));

        Assert.Contains("Pathbuilder", notJson.Message);
        Assert.Contains("no character name", noName.Message);
        Assert.False((await Read("GNIB11")).Exists, "a failed paste must not start a table");
    }

    [Fact]
    public async Task AnUnknownCodeIsATableWaitingToBeStartedRatherThanAMissingOne()
    {
        var table = await Read("NOBODY");

        Assert.False(table.Exists);
        Assert.Empty(table.Characters);
    }

    [Fact]
    public async Task ARuleRecordStatingModifiersIsReadThroughTheSummaryAndTheDetail()
    {
        await using var db = database.NewContext();

        // Every seeded record answers empty today, so the read path the owner's ingest work
        // fills is proved against a record carrying the shape that work will emit.
        db.RuleRecords.Add(new RuleRecord
        {
            Id = "spell-courageous-anthem-test",
            Category = "spell-with-modifiers",
            Name = "Courageous Anthem",
            SourceUrl = "https://2e.aonprd.com/Spells.aspx?ID=1",
            RulesetVersion = "test",
            Mechanics = """
                {"modifiers":[{"type":"Status","value":1,
                  "applies":[{"kind":"Exactly","stat":"Attack"},{"kind":"Exactly","stat":"Damage"}]}]}
                """,
        });
        await db.SaveChangesAsync();

        var found = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "spell-with-modifiers"), default);
        var detail = await new GetRuleHandler(db).Handle(new GetRule("spell-courageous-anthem-test"), default);

        Assert.True(Assert.Single(found.Items).HasModifiers);

        var modifier = Assert.Single(detail!.Modifiers);
        Assert.Equal("Status", modifier.Type);
        Assert.Equal(1, modifier.Value);
        Assert.Equal(["Attack", "Damage"], modifier.Applies.Select(a => a.Stat));
    }

    [Fact]
    public async Task ASeededRecordStatesNoModifierThisAppCanApplyYet()
    {
        await using var db = database.NewContext();

        var conditions = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "condition", PageSize: 200), default);

        Assert.NotEmpty(conditions.Items);
        Assert.All(conditions.Items, item => Assert.False(item.HasModifiers));
    }

    // design/004 makes the hit-point operation a delta precisely so two people applying damage
    // at the same moment sum instead of clobbering each other. A delta on the wire is necessary
    // and not sufficient: a handler that reads, adds and writes back puts last-write-wins one
    // layer lower, where the sequential test above cannot see it.
    [Fact]
    public async Task ConcurrentHitPointChangesAllLandRatherThanClobberingEachOther()
    {
        var character = await Import("GNIB20", Fixture("gnibbo.json"));
        const int blows = 20;

        await Task.WhenAll(Enumerable.Range(0, blows)
            .Select(_ => Task.Run(() => Damage("GNIB20", character.Id, -1))));

        Assert.Equal(
            character.CurrentHitPoints - blows,
            Assert.Single((await Read("GNIB20")).Characters).CurrentHitPoints);
    }

    // Doing the arithmetic in the database leaves the instance the handler holds stale, and a
    // re-query does not fix it, because identity resolution hands back the object already
    // tracked. Reading the table afterwards cannot catch that: the row is right and only the
    // answer is wrong, so this asserts the answer against the row.
    [Fact]
    public async Task ACommandAnswersWithTheNumberTheDatabaseEndsUpHolding()
    {
        var character = await Import("GNIB21", Fixture("gnibbo.json"));

        var answered = await Damage("GNIB21", character.Id, -9);

        Assert.Equal(
            Assert.Single((await Read("GNIB21")).Characters).CurrentHitPoints,
            answered!.CurrentHitPoints);
    }

    // The slot id is client-generated so a retry over a flaky table connection converges instead
    // of stacking duplicates. A retry is exactly what arrives twice at once, so two requests can
    // both find the slot empty and both insert the same key.
    [Fact]
    public async Task ConcurrentAppliesToOneSlotAllSucceedAndLeaveOneEffect()
    {
        var character = await Import("GNIB22", Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();
        var spec = new EffectSpec("Clumsy", "Seeded", "clumsy", 2, null, []);

        var answers = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => Set("GNIB22", character.Id, slot, spec))));

        Assert.All(answers, answer => Assert.Single(answer!.Effects));
        Assert.Single(Assert.Single((await Read("GNIB22")).Characters).Effects);
    }
}
