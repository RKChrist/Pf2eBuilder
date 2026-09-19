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
}
