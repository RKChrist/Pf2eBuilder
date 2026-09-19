using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Features.Conditions;
using Pf2e.Application.Features.Rules;
using Pf2e.Domain;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

public class SeedingRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    // The oracle. These are the counts the transform emits from the tracked snapshot, and they
    // are written out rather than read from the seed files, so a change to the ingest has to be
    // acknowledged here deliberately instead of passing by construction.
    static readonly Dictionary<string, int> Published = new(StringComparer.Ordinal)
    {
        ["action"] = 551, ["ancestry"] = 67, ["animal-companion"] = 97,
        ["animal-companion-advanced"] = 5, ["animal-companion-specialization"] = 11,
        ["animal-companion-unique"] = 2, ["apparition"] = 14, ["arcane-school"] = 27,
        ["arcane-thesis"] = 5, ["archetype"] = 248, ["armor"] = 38, ["armor-group"] = 7,
        ["background"] = 524, ["bloodline"] = 18, ["cause"] = 7, ["class"] = 29,
        ["class-feature"] = 774, ["class-kit"] = 32, ["condition"] = 56, ["conscious-mind"] = 6,
        ["creature"] = 3786, ["creature-ability"] = 45, ["creature-family"] = 441, ["curse"] = 56,
        ["deity"] = 484, ["deity-category"] = 40, ["deviant-ability-classification"] = 10,
        ["doctrine"] = 3, ["domain"] = 61, ["draconic-exemplar"] = 44, ["druidic-order"] = 9,
        ["eidolon"] = 26, ["element"] = 6, ["epithet"] = 18, ["equipment"] = 6469,
        ["familiar-ability"] = 141, ["familiar-specific"] = 37, ["fatal-method"] = 2,
        ["feat"] = 6359, ["follower"] = 6, ["grim-fascination"] = 4, ["hellknight-order"] = 14,
        ["heritage"] = 308, ["hunters-edge"] = 4, ["hybrid-study"] = 15, ["ikon"] = 21,
        ["implement"] = 10, ["innovation"] = 7, ["instinct"] = 10, ["item-bonus"] = 987,
        ["language"] = 121, ["lesson"] = 19, ["methodology"] = 5, ["muse"] = 5, ["mystery"] = 12,
        ["mythic-calling"] = 15, ["patron"] = 17, ["practice"] = 4, ["racket"] = 6,
        ["relic"] = 122, ["research-field"] = 4, ["ritual"] = 163, ["runesmith-rune"] = 44,
        ["set-relic"] = 14, ["shield"] = 16, ["skill"] = 33, ["skill-general-action"] = 19,
        ["source"] = 254, ["spell"] = 1811, ["style"] = 6, ["subconscious-mind"] = 4,
        ["tactic"] = 37, ["tradition"] = 5, ["trait"] = 564, ["way"] = 11, ["weapon"] = 326,
        ["weapon-group"] = 17,
    };

    [Fact]
    public async Task FreshDatabaseHoldsEveryPublishedRecord()
    {
        await using var db = database.NewContext();

        var actual = await db.RuleRecords
            .GroupBy(r => r.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);

        Assert.Equal(Published.Count, actual.Count);
        foreach (var (category, expected) in Published)
        {
            Assert.True(actual.TryGetValue(category, out var count), $"{category} was not seeded at all");
            Assert.Equal(expected, count);
        }

        Assert.Equal(Published.Values.Sum(), await db.RuleRecords.CountAsync());
    }

    [Fact]
    public async Task SeedingTwiceChangesNothingAndDoesNoWork()
    {
        Assert.False(database.FirstRun.Skipped, "the first run must actually seed");
        Assert.True(database.SecondRun.Skipped, "the second run must recognise the database is current");
        Assert.Equal(database.FirstRun.RecordCount, database.SecondRun.RecordCount);

        await using var db = database.NewContext();
        Assert.Equal(Published.Values.Sum(), await db.RuleRecords.CountAsync());
        Assert.Equal(1, await db.SeedState.CountAsync());
    }

    [Fact]
    public async Task ASeedWhoseValuesChangeAtTheSameCountIsSeededAgain()
    {
        var seed = Directory.CreateTempSubdirectory("pf2e-seed-").FullName;
        var file = Path.Combine(Path.GetTempPath(), $"pf2e-reseed-{Guid.NewGuid():N}.db");
        File.Copy(Path.Combine(SeededDatabase.SeedPath, "condition.json"), Path.Combine(seed, "condition.json"));
        RulesDbContext Open() => new(new DbContextOptionsBuilder<RulesDbContext>().UseSqlite($"Data Source={file}").Options);

        await using (var db = Open())
        {
            await db.Database.MigrateAsync();
            Assert.False((await SeededDatabase.SeederFor(db, seed).SeedAsync()).Skipped);
        }

        var conditions = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(seed, "condition.json")))!.AsArray();
        var first = conditions[0]!.AsObject();
        var id = first["id"]!.GetValue<string>();
        first["name"] = "Renamed by the transform";
        await File.WriteAllTextAsync(Path.Combine(seed, "condition.json"), conditions.ToJsonString());

        await using (var db = Open())
        {
            var again = await SeededDatabase.SeederFor(db, seed).SeedAsync();
            Assert.False(again.Skipped, "a changed value at an unchanged count must reseed");
            Assert.Equal(conditions.Count, again.RecordCount);
        }

        await using (var db = Open())
        {
            Assert.Equal("Renamed by the transform", (await db.RuleRecords.SingleAsync(r => r.Id == id)).Name);
            Assert.True((await SeededDatabase.SeederFor(db, seed).SeedAsync()).Skipped);
        }

        SqliteConnectionPool.Clear();
        File.Delete(file);
        Directory.Delete(seed, recursive: true);
    }

    [Fact]
    public async Task PromotedFieldsAreNotAlsoStoredInTheJsonBlob()
    {
        await using var db = database.NewContext();

        var sample = await db.RuleRecords
            .Where(r => r.Category == "feat" || r.Category == "equipment" || r.Category == "condition")
            .Take(400)
            .ToListAsync();

        foreach (var record in sample)
        {
            var mechanics = JsonNode.Parse(record.Mechanics)!.AsObject();
            foreach (var promoted in new[] { "id", "name", "category", "sourceUrl", "level", "rarity", "type", "primary_source", "trait" })
            {
                Assert.False(mechanics.ContainsKey(promoted), $"{record.Id} stores '{promoted}' twice");
            }
        }
    }

    [Fact]
    public async Task SeedingRefusesToStartWithoutSeedFiles()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"pf2e-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);

        await using var db = database.NewContext();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeededDatabase.SeederFor(db, empty).SeedAsync());

        Assert.Contains(empty, failure.Message);
        Assert.Contains("rules-import", failure.Message);
        Directory.Delete(empty);
    }

    [Fact]
    public async Task TraitsSurviveTheRoundTripAsAList()
    {
        await using var db = database.NewContext();

        var withTraits = await db.RuleRecords
            .Where(r => r.Category == "feat")
            .OrderBy(r => r.Id)
            .Take(200)
            .ToListAsync();

        var traited = withTraits.Where(r => r.Traits.Count > 0).ToList();
        Assert.NotEmpty(traited);
        Assert.All(traited, r => Assert.All(r.Traits, t => Assert.False(string.IsNullOrWhiteSpace(t))));
    }

    [Fact]
    public async Task EveryRecordCarriesItsRulesetVersionAndASourceUrl()
    {
        await using var db = database.NewContext();

        Assert.Equal(0, await db.RuleRecords.CountAsync(r => r.RulesetVersion != "aon-20260902-190924"));
        Assert.Equal(0, await db.RuleRecords.CountAsync(r => !r.SourceUrl.StartsWith("https://2e.aonprd.com/")));
    }
}
