using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Features.Conditions;
using Pf2e.Application.Features.Rules;
using Pf2e.Domain;

namespace Pf2e.Persistence.Tests;

public class SeedingRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    // The oracle. These are the counts the transform emits from the tracked snapshot, and they
    // are written out rather than read from the seed files, so a change to the ingest has to be
    // acknowledged here deliberately instead of passing by construction.
    static readonly Dictionary<string, int> Published = new(StringComparer.Ordinal)
    {
        ["action"] = 3921, ["ancestry"] = 67, ["archetype"] = 251, ["armor"] = 42,
        ["background"] = 524, ["class"] = 29, ["class-feature"] = 774, ["condition"] = 56,
        ["deity"] = 484, ["equipment"] = 6568, ["feat"] = 6390, ["heritage"] = 337,
        ["language"] = 121, ["ritual"] = 163, ["skill"] = 33, ["spell"] = 1836,
        ["trait"] = 564, ["weapon"] = 372,
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
