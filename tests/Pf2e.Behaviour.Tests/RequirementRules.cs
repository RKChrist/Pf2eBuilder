using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// Reading a stated requirement back into something a sheet can check. Only the plain proficiency
/// phrases parse, and everything else stays a sentence on purpose.
/// </summary>
public class RequirementRules
{
    [Theory]
    [InlineData("trained in Stealth", ProficiencyRank.Trained, "Stealth")]
    [InlineData("expert in Perception", ProficiencyRank.Expert, "Perception")]
    [InlineData("master in Medicine", ProficiencyRank.Master, "Medicine")]
    [InlineData("legendary in Thievery", ProficiencyRank.Legendary, "Thievery")]
    [InlineData("trained in Cooking Lore", ProficiencyRank.Trained, "Cooking Lore")]
    public void APlainPhraseReadsBackAsARankAndASkill(string text, ProficiencyRank rank, string skill)
    {
        var parsed = Requirements.Parse(text);

        Assert.NotNull(parsed);
        Assert.Equal(rank, parsed!.Value.Rank);
        Assert.Equal(skill, Assert.Single(parsed.Value.Skills));
    }

    [Fact]
    public void AListOfSkillsIsEveryOneOfThem()
    {
        var parsed = Requirements.Parse("trained in Diplomacy, Intimidation, or Performance");

        Assert.NotNull(parsed);
        Assert.Equal(["Diplomacy", "Intimidation", "Performance"], parsed!.Value.Skills);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("You are wielding a shield.")]
    [InlineData("knowledge of the recipe")]
    [InlineData("The companion to be learned from must be camping with you.")]
    [InlineData("Your last action was to Raise a Shield.")]
    public void AnythingElseStaysASentence(string? text)
    {
        // A green tick beside "the companion must be camping with you" would be the app deciding
        // something the table decides.
        Assert.Null(Requirements.Parse(text));
    }

    [Fact]
    public void MeetingItNeedsOnlyOneOfTheSkillsItNames()
    {
        var parsed = Requirements.Parse("trained in Arcana, Occultism, Religion, or Society")!.Value;

        Assert.True(Requirements.MetBy(parsed, skill => skill == "Society" ? ProficiencyRank.Trained : ProficiencyRank.Untrained));
        Assert.False(Requirements.MetBy(parsed, _ => ProficiencyRank.Untrained));
    }

    [Fact]
    public void AndABetterRankThanAskedForStillMeetsIt()
    {
        var parsed = Requirements.Parse("trained in Stealth")!.Value;

        Assert.True(Requirements.MetBy(parsed, _ => ProficiencyRank.Legendary));
        Assert.False(Requirements.MetBy(Requirements.Parse("expert in Stealth")!.Value, _ => ProficiencyRank.Trained));
    }

    [Fact]
    public void EveryCampingRequirementInTheRulesetEitherParsesOrIsLeftAlone()
    {
        // The four that parse, from the Kingmaker camping activities, and one that does not.
        Assert.NotNull(Requirements.Parse("trained in Stealth"));
        Assert.NotNull(Requirements.Parse("trained in Survival"));
        Assert.NotNull(Requirements.Parse("trained in Cooking Lore"));
        Assert.NotNull(Requirements.Parse("expert in Perception"));
        Assert.Null(Requirements.Parse("knowledge of the recipe"));
    }
}
