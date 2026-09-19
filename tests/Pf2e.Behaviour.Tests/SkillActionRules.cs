using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// What a character can attempt. Hand-written against the printed rules, like the buffs, because
/// Archives of Nethys publishes an action's name and its traits and not its requirement.
/// </summary>
public class SkillActionRules
{
    static readonly string[] RealSkills =
    [
        "Acrobatics", "Arcana", "Athletics", "Crafting", "Deception", "Diplomacy",
        "Intimidation", "Medicine", "Nature", "Occultism", "Performance", "Religion",
        "Society", "Stealth", "Survival", "Thievery",
    ];

    public static TheoryData<string> EveryAction => [.. SkillActions.All.Select(a => a.Key)];

    [Theory]
    [MemberData(nameof(EveryAction))]
    public void EveryActionNamesSkillsTheEngineKnows(string key)
    {
        var action = SkillActions.Find(key)!;

        Assert.NotEmpty(action.Skills);
        Assert.All(action.Skills, skill =>
            // Perception is not a skill and Seek is rolled with it, so it is allowed by name.
            Assert.True(
                skill == "Perception" || (RealSkills.Contains(skill) && Skills.For(skill) is not null),
                $"{action.Name} names {skill}"));
    }

    [Theory]
    [MemberData(nameof(EveryAction))]
    public void EveryActionSaysWhatItIsAndWhereItCameFrom(string key)
    {
        var action = SkillActions.Find(key)!;

        Assert.True(action.When.Length > 15, action.Name);
        Assert.EndsWith(".", action.When);
        Assert.Equal("Player Core, Skills", action.SourceRef);

        // Nobody has checked these against the book, and the app should not pretend otherwise.
        Assert.False(action.Verified, action.Name);
    }

    [Fact]
    public void AnUntrainedActionIsOpenToAnybody()
    {
        var seek = SkillActions.Find("seek")!;

        Assert.True(seek.MetBy(_ => ProficiencyRank.Untrained));
    }

    [Fact]
    public void ATrainedActionIsNotUntilTheyAreTrained()
    {
        var pick = SkillActions.Find("pick-a-lock")!;

        Assert.False(pick.MetBy(_ => ProficiencyRank.Untrained));
        Assert.True(pick.MetBy(_ => ProficiencyRank.Trained));
        Assert.True(pick.MetBy(_ => ProficiencyRank.Legendary));
    }

    [Fact]
    public void AnActionWithSeveralSkillsNeedsTrainingInOnlyOneOfThem()
    {
        // This is the design document's own worked example: a character can Decipher Writing
        // because their sheet says trained in Society, whatever their Arcana says.
        var decipher = SkillActions.Find("decipher-writing")!;

        Assert.Equal(4, decipher.Skills.Length);
        Assert.True(decipher.MetBy(skill => skill == "Society" ? ProficiencyRank.Trained : ProficiencyRank.Untrained));
        Assert.False(decipher.MetBy(_ => ProficiencyRank.Untrained));
    }

    [Fact]
    public void AndIsRolledWithTheBestOfThemRatherThanTheFirst()
    {
        var decipher = SkillActions.Find("decipher-writing")!;

        var best = decipher.Best(
            skill => skill == "Religion" ? ProficiencyRank.Expert : ProficiencyRank.Trained,
            skill => skill == "Religion" ? 14 : 20);

        // Rank first, because a higher rank is what the rules care about, and then the number.
        Assert.Equal("Religion", best);

        var tied = decipher.Best(_ => ProficiencyRank.Trained, skill => skill == "Occultism" ? 18 : 9);
        Assert.Equal("Occultism", tied);
    }

    [Fact]
    public void TheListCoversWhatATableReachesForRatherThanEveryPrintedAction()
    {
        // The catalogue already lists all 551 actions. This is the subset somebody scans before
        // opening a door, so it is short on purpose and has both halves of the question in it.
        Assert.InRange(SkillActions.All.Length, 30, 60);
        Assert.Contains(SkillActions.All, action => action.Required is ProficiencyRank.Untrained);
        Assert.Contains(SkillActions.All, action => action.Required is ProficiencyRank.Trained);

        var keys = SkillActions.All.Select(a => a.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
