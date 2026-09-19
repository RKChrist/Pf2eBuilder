using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Pf2e.Application.Features.Conditions;
using Pf2e.Application.Features.Rules;
using Pf2e.Domain;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// Exercises the query handlers against a real seeded database, because a handler that works
/// against a substitute proves nothing about the query it actually runs.
/// </summary>
public class HandlerRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    [Fact]
    public async Task EveryCodedConditionResolvesToItsSeededRule()
    {
        await using var db = database.NewContext();

        var conditions = await new GetConditionsHandler(db).Handle(new GetConditions(), default);

        Assert.Equal(Conditions.All.Length, conditions.Count);
        var unlinked = conditions.Where(c => c.SourceUrl is null).Select(c => c.Key).ToArray();
        Assert.True(unlinked.Length == 0, $"conditions with no rule link: {string.Join(", ", unlinked)}");
        Assert.All(conditions, c => Assert.StartsWith("https://2e.aonprd.com/", c.SourceUrl!));
    }

    [Fact]
    public async Task AConditionCarriesItsModifiersDescribedForAReader()
    {
        await using var db = database.NewContext();

        var conditions = await new GetConditionsHandler(db).Handle(new GetConditions(), default);
        var clumsy = conditions.Single(c => c.Key == "clumsy");

        Assert.True(clumsy.HasValue);
        Assert.False(clumsy.Verified);
        Assert.Contains("Dexterity-based", Assert.Single(clumsy.Modifiers).Applies);

        var encumbered = conditions.Single(c => c.Key == "encumbered");
        Assert.Equal(2, encumbered.Modifiers.Count);
        Assert.Contains(encumbered.Modifiers, m => m.Applies.Contains("speed"));
    }

    [Fact]
    public async Task SearchingByCategoryAndLevelReturnsOnlyMatchesAndTheTrueTotal()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "feat", MinLevel: 1, MaxLevel: 1, PageSize: 10), default);

        Assert.Equal(10, result.Items.Count);
        Assert.True(result.TotalMatching > 10, "a page is not the whole result set");
        Assert.All(result.Items, item =>
        {
            Assert.Equal("feat", item.Category);
            Assert.Equal(1, item.Level);
        });
    }

    [Fact]
    public async Task SearchingByTraitFiltersOnTraitsRatherThanNames()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "feat", Trait: "Goblin", PageSize: 200), default);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
            Assert.Contains("Goblin", item.Traits, StringComparer.OrdinalIgnoreCase));

        // Paging after a trait filter must not drop matches, so the total is the filtered count.
        Assert.Equal(result.Items.Count, Math.Min(result.TotalMatching, 200));
    }

    [Fact]
    public async Task PagingIsStableAndDoesNotRepeatOrSkipRecords()
    {
        await using var db = database.NewContext();
        var handler = new SearchRulesHandler(db);

        var first = await handler.Handle(new SearchRules(Category: "class", Page: 1, PageSize: 10), default);
        var second = await handler.Handle(new SearchRules(Category: "class", Page: 2, PageSize: 10), default);

        Assert.Equal(29, first.TotalMatching);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
    }

    [Theory]
    [InlineData(0, 50, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 500, "PageSize")]
    public void AnOutOfRangeQueryIsRejectedBeforeItReachesTheDatabase(int page, int pageSize, string field)
    {
        var failures = new SearchRulesValidator().Validate(new SearchRules(Page: page, PageSize: pageSize));

        Assert.False(failures.IsValid);
        Assert.Contains(failures.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void AnInvertedLevelRangeIsRejected()
    {
        var failures = new SearchRulesValidator().Validate(new SearchRules(MinLevel: 9, MaxLevel: 2));

        Assert.False(failures.IsValid);
        Assert.Contains(failures.Errors, e => e.ErrorMessage.Contains("MinLevel must not exceed MaxLevel"));
    }

    [Theory]
    [InlineData("bloodline", "sorcerer")]
    [InlineData("instinct", "barbarian")]
    [InlineData("doctrine", "cleric")]
    [InlineData("racket", "rogue")]
    [InlineData("muse", "bard")]
    [InlineData("hunters-edge", "ranger")]
    [InlineData("research-field", "alchemist")]
    public async Task ASubclassChoiceHasRealOptionsAndNotJustAnEmptySlot(string category, string forClass)
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: category, PageSize: 200), default);

        // A class-feature record holds the slot. These hold the options a player picks from,
        // and without them the builder cannot produce a legal character of that class.
        Assert.True(result.TotalMatching > 1,
            $"a {forClass} needs real {category} options, found {result.TotalMatching}");
        Assert.All(result.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
    }

    [Fact]
    public async Task ShieldsCarryTheArmourClassBonusThatExistsNowhereElse()
    {
        await using var db = database.NewContext();

        var shields = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "shield", PageSize: 200), default);

        Assert.NotEmpty(shields.Items);
        var buckler = shields.Items.FirstOrDefault(s => s.Name.Contains("Buckler", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(buckler);

        var record = await db.RuleRecords.FindAsync(buckler!.Id);
        Assert.Contains("\"ac\"", record!.Mechanics);
        Assert.Contains("\"hardness\"", record.Mechanics);
    }

    [Fact]
    public async Task ItemBonusRecordsCarryTheNumberTheEngineNeeds()
    {
        await using var db = database.NewContext();

        var bonuses = await db.RuleRecords
            .Where(r => r.Category == "item-bonus")
            .Take(50)
            .ToListAsync();

        Assert.NotEmpty(bonuses);
        // Only the highest item bonus applies, so the engine needs the value, and the parent
        // equipment record has an empty skill_mod on every row in the snapshot.
        Assert.All(bonuses, b => Assert.Contains("item_bonus_value", b.Mechanics));
    }

    static int Rank(string name, string query) =>
        name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0
        : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    [Fact]
    public async Task ANameSearchPutsTheExactNameFirstThenPrefixesThenTheRest()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db).Handle(new SearchRules(Name: "shield", PageSize: 200), default);

        Assert.Equal("shield", result.Items[0].Name, ignoreCase: true);
        var ranks = result.Items.Select(item => Rank(item.Name, "shield")).ToList();
        Assert.Equal(ranks.Order(), ranks);
        Assert.Contains(1, ranks);
        Assert.Contains(2, ranks);
    }

    [Fact]
    public async Task ATraitFilteredNameSearchRanksTheSameWay()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "feat", Name: "shield", Trait: "Fighter", PageSize: 200), default);

        Assert.NotEmpty(result.Items);
        var ranks = result.Items.Select(item => Rank(item.Name, "shield")).ToList();
        Assert.Equal(ranks.Order(), ranks);
        Assert.Contains(1, ranks);
        Assert.All(result.Items, item => Assert.Contains("Fighter", item.Traits, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AWildcardTypedIntoTheNameIsMatchedLiterally()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db).Handle(new SearchRules(Name: "%"), default);

        Assert.Equal(0, result.TotalMatching);
    }

    [Fact]
    public async Task UnfilteredCountsAreEveryCategorysSize()
    {
        await using var db = database.NewContext();

        var counts = await new CountRulesHandler(db).Handle(new CountRules(), default);

        Assert.Equal(await db.RuleRecords.CountAsync(), counts.Total);
        Assert.Equal(74, counts.Categories.Count);
        Assert.Equal(6390, counts.Categories.Single(c => c.Category == "feat").Count);
        Assert.Equal(counts.Total, counts.Categories.Sum(c => c.Count));
    }

    [Fact]
    public async Task ANameCountAgreesWithTheSearchItStandsFor()
    {
        await using var db = database.NewContext();

        var counts = await new CountRulesHandler(db).Handle(new CountRules(Name: "shield"), default);
        var search = new SearchRulesHandler(db);

        Assert.Equal((await search.Handle(new SearchRules(Name: "shield"), default)).TotalMatching, counts.Total);
        foreach (var category in counts.Categories)
        {
            var listed = await search.Handle(new SearchRules(Category: category.Category, Name: "shield"), default);
            Assert.Equal(listed.TotalMatching, category.Count);
        }
    }

    [Fact]
    public async Task ATraitCountSaysWhereTheTraitIsUsed()
    {
        await using var db = database.NewContext();

        var counts = await new CountRulesHandler(db).Handle(new CountRules(Trait: "manipulate"), default);
        var search = new SearchRulesHandler(db);

        Assert.True(counts.Categories.Count > 1, "manipulate is on more than one kind of record");
        Assert.All(counts.Categories, c => Assert.True(c.Count > 0));
        foreach (var category in counts.Categories)
        {
            var listed = await search.Handle(new SearchRules(Category: category.Category, Trait: "Manipulate"), default);
            Assert.Equal(listed.TotalMatching, category.Count);
        }
        Assert.Equal(counts.Categories.Sum(c => c.Count), counts.Total);
    }

    [Fact]
    public void AnEmptyTraitIsRejectedByTheCount()
    {
        var failures = new CountRulesValidator().Validate(new CountRules(Trait: ""));

        Assert.False(failures.IsValid);
    }

    [Fact]
    public async Task AWeaponRowCarriesItsDamageCategoryGroupAndHands()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "weapon", Name: "Longsword"), default);
        var longsword = result.Items[0];

        Assert.Equal("Longsword", longsword.Name);
        Assert.Equal(
            ["damage=1d8 S", "weapon_category=Martial", "weapon_group=Sword", "hands=1"],
            longsword.Highlights.Select(h => $"{h.Key}={string.Join(",", h.Values)}"));
    }

    [Fact]
    public async Task AFeatNeverHighlightsItsOwnNameAndNoRowSaysMoreThanFourThings()
    {
        await using var db = database.NewContext();

        var feats = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "feat", PageSize: 200), default);

        Assert.All(feats.Items, feat =>
        {
            Assert.InRange(feat.Highlights.Count, 0, 4);
            Assert.DoesNotContain(feat.Highlights, h => h.Key == "feat");
            Assert.All(feat.Highlights, h => Assert.NotEmpty(h.Values));
        });
        Assert.Contains(feats.Items, feat => feat.Highlights.Any(h => h.Key == "prerequisite"));
    }

    [Fact]
    public async Task AReactionFeatLeadsWithItsCostAndTrigger()
    {
        await using var db = database.NewContext();

        var result = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "feat", Name: "Reactive Shield"), default);
        var keys = result.Items[0].Highlights.Select(h => h.Key).ToList();

        Assert.Equal(["actions", "archetype", "trigger"], keys);
        Assert.Equal(["Reaction"], result.Items[0].Highlights[0].Values);
    }

    [Fact]
    public async Task ABackgroundKeepsTheFeatItGrants()
    {
        await using var db = database.NewContext();

        var backgrounds = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "background", PageSize: 200), default);

        Assert.Contains(backgrounds.Items, b => b.Highlights.Any(h => h.Key == "feat"));
    }

    [Fact]
    public async Task ATraitGroupIsListedOnceEvenWhereTheSeedRepeatsIt()
    {
        await using var db = database.NewContext();

        var arcane = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "trait", Name: "Arcane"), default);
        var group = Assert.Single(arcane.Items[0].Highlights);

        Assert.Equal("trait_group", group.Key);
        Assert.Equal(group.Values.Distinct(StringComparer.OrdinalIgnoreCase), group.Values);
    }

    [Fact]
    public async Task ACategoryWithNothingWorthAGlanceHighlightsNothing()
    {
        await using var db = database.NewContext();

        var conditions = await new SearchRulesHandler(db).Handle(new SearchRules(Category: "condition"), default);

        Assert.All(conditions.Items, c => Assert.Empty(c.Highlights));
    }
}
