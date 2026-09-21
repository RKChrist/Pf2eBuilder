using Pf2e.Application.Features.Rules;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The camping activities, read out of the ruleset rather than written into this codebase. They
/// are seeded actions carrying the Camping trait, and four of the twenty-three print a
/// requirement a sheet can check.
/// </summary>
public class CampingActivityRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    async Task<IReadOnlyList<Pf2e.Contracts.Tracker.CampingActivityView>> Camping()
    {
        await using var db = database.NewContext();
        return await new GetCampingActivitiesHandler(db).Handle(new GetCampingActivities(), default);
    }

    [Fact]
    public async Task EveryCampingActivityInTheRulesetIsOffered()
    {
        var camping = await Camping();

        Assert.Equal(23, camping.Count);

        var names = camping.Select(a => a.Name).ToList();
        foreach (var expected in new[]
        {
            "Camouflage Campsite", "Cook Basic Meal", "Hunt and Gather", "Organize Watch",
            "Set Traps", "Tell Campfire Story", "Blend into the Night",
        })
        {
            Assert.Contains(expected, names);
        }

        // Alphabetical, because this is a list somebody scans for a name they already have in mind.
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    }

    [Fact]
    public async Task AndEachOneOpensItsRule()
    {
        var camping = await Camping();

        Assert.All(camping, activity => Assert.StartsWith("action-", activity.RuleId));

        await using var db = database.NewContext();
        var first = camping.First();
        var record = await new GetRuleHandler(db).Handle(new GetRule(first.RuleId), default);

        Assert.Equal(first.Name, record!.Summary.Name);
    }

    [Fact]
    public async Task APlainProficiencyRequirementIsReadBackForChecking()
    {
        var camping = await Camping();

        var camouflage = camping.Single(a => a.Name == "Camouflage Campsite");
        Assert.Equal("trained in Stealth", camouflage.Requires);
        Assert.Equal("Trained", camouflage.Rank);
        Assert.Equal("Stealth", Assert.Single(camouflage.Skills));

        var watch = camping.Single(a => a.Name == "Organize Watch");
        Assert.Equal("Expert", watch.Rank);
        Assert.Equal("Perception", Assert.Single(watch.Skills));

        // A Lore is a skill a character writes down, so it is kept as named.
        var meal = camping.Single(a => a.Name == "Discover Special Meal");
        Assert.Equal("Cooking Lore", Assert.Single(meal.Skills));
    }

    [Fact]
    public async Task AndAnythingElseStaysASentenceNobodyTicks()
    {
        var camping = await Camping();

        var recipe = camping.Single(a => a.Name == "Cook Special Meal");
        Assert.Equal("knowledge of the recipe", recipe.Requires);
        Assert.Null(recipe.Rank);
        Assert.Empty(recipe.Skills);

        // Most of them print no requirement at all, and that is not the same as meeting one.
        var story = camping.Single(a => a.Name == "Tell Campfire Story");
        Assert.Null(story.Requires);
        Assert.Null(story.Rank);
    }

    [Fact]
    public async Task ExactlyFourOfThemCanBeCheckedAgainstASheet()
    {
        var camping = await Camping();

        var checkable = camping.Where(a => a.Rank is not null).Select(a => a.Name).ToList();

        Assert.Equal(
            ["Camouflage Campsite", "Discover Special Meal", "Hunt and Gather", "Organize Watch"],
            checkable.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AndTheCampsiteMealsComeOutOfTheRulesetTheSameWay()
    {
        await using var db = database.NewContext();
        var meals = await new GetCampsiteMealsHandler(db).Handle(new GetCampsiteMeals(), default);

        Assert.Equal(27, meals.Count);
        Assert.All(meals, meal => Assert.NotEmpty(meal.Name));
        // Every one of them carries a level today, which is the assertion that would catch a seed
        // dropping one. Level zero is a real meal, so a missing level must not read as one.
        Assert.All(meals, meal => Assert.NotNull(meal.Level));
        Assert.All(meals, meal => Assert.InRange(meal.Level!.Value, 0, 20));

        var names = meals.Select(meal => meal.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);

        // Seven of the twenty-seven print a requirement, and the rest print none, which is not the
        // same as printing one anybody meets.
        Assert.Equal(7, meals.Count(meal => meal.Requires is not null));
        Assert.Equal(
            "legendary in Arcana or Nature",
            meals.Single(meal => meal.RuleId == "campsite-meal-2").Requires);
    }
}
