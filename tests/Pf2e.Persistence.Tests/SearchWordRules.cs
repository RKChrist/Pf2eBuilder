using Pf2e.Application.Features.Rules;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// What a search box has to forgive. The owner typed "raise shield" and was told the ruleset had
/// nothing, because the action is Raise a Shield and the search wanted the phrase as one run of
/// characters.
/// </summary>
public class SearchWordRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    async Task<Pf2e.Contracts.Rules.RuleSearchResult> Search(string name, string? category = null)
    {
        await using var db = database.NewContext();
        return await new SearchRulesHandler(db).Handle(new SearchRules(category, name, PageSize: 10), default);
    }

    [Theory]
    [InlineData("raise shield")]
    [InlineData("shield raise")]
    [InlineData("  RAISE   a  shield ")]
    public async Task EveryWordTypedIsLookedForAndTheArticleNobodyTypesIsNotMissed(string typed)
    {
        var found = await Search(typed);

        Assert.Contains(found.Items, rule => rule.Name == "Raise a Shield");
        Assert.Null(found.SearchedFor);
    }

    [Theory]
    [InlineData("raise shiedl", "raise shield", "Raise a Shield")]
    [InlineData("fierball", "fireball", "Fireball")]
    [InlineData("frigthened", "frightened", "Frightened")]
    public async Task AWordThatIsInNoNameAtAllIsReadAsTheClosestOneThatIs(string typed, string read, string finds)
    {
        var found = await Search(typed);

        Assert.Equal(read, found.SearchedFor);
        Assert.Contains(found.Items, rule => rule.Name == finds);
    }

    [Fact]
    public async Task AWordThatMatchesSomethingIsNeverSecondGuessed()
    {
        // "shie" is not a word and is the start of one, which is how everybody searches.
        var found = await Search("shie");

        Assert.Null(found.SearchedFor);
        Assert.Contains(found.Items, rule => rule.Name == "Shield");
    }

    [Fact]
    public async Task NonsenseStillFindsNothingAndSaysSo()
    {
        var found = await Search("zzqxv");

        Assert.Empty(found.Items);
        Assert.Null(found.SearchedFor);
    }

    [Fact]
    public async Task TheCountsBesideTheListReadTheTypoTheSameWay()
    {
        await using var db = database.NewContext();
        var counts = await new CountRulesHandler(db).Handle(new CountRules("raise shiedl", null), default);

        Assert.Equal((await Search("raise shiedl")).TotalMatching, counts.Total);
        Assert.True(counts.Total > 0);
    }
}
