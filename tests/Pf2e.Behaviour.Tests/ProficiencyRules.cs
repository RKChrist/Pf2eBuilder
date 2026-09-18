using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

public class ProficiencyRules
{
    [Fact]
    public void UntrainedProficiencyDoesNotAddLevel()
    {
        Assert.Equal(0, Proficiency.Bonus(ProficiencyRank.Untrained, level: 7));
        Assert.Equal(0, Proficiency.Bonus(ProficiencyRank.Untrained, level: 20));
    }

    [Fact]
    public void TrainedProficiencyAddsLevelPlusTwo()
    {
        Assert.Equal(9, Proficiency.Bonus(ProficiencyRank.Trained, level: 7));
    }

    [Theory]
    [InlineData(ProficiencyRank.Expert, 11)]
    [InlineData(ProficiencyRank.Master, 13)]
    [InlineData(ProficiencyRank.Legendary, 15)]
    public void HigherRanksAddLevelPlusTheirStep(ProficiencyRank rank, int expected)
    {
        Assert.Equal(expected, Proficiency.Bonus(rank, level: 7));
    }

    [Fact]
    public void ProficiencyWithoutLevelRemovesLevelFromEveryCheck()
    {
        var variant = new RuleOptions(ProficiencyWithoutLevel: true);

        foreach (var rank in Enum.GetValues<ProficiencyRank>())
        {
            Assert.True(
                Proficiency.Bonus(rank, level: 1, variant) == Proficiency.Bonus(rank, level: 20, variant),
                $"{rank} must give the same bonus at level 1 and level 20 under Proficiency Without Level");
        }

        Assert.Equal(2, Proficiency.Bonus(ProficiencyRank.Trained, level: 7, variant));
        Assert.Equal(8, Proficiency.Bonus(ProficiencyRank.Legendary, level: 7, variant));

        // UNVERIFIED: the untrained value of -2 comes from the design brief, not from GM Core.
        // Check it against the printed Proficiency Without Level variant before trusting it.
        Assert.Equal(-2, Proficiency.Bonus(ProficiencyRank.Untrained, level: 7, variant));
    }
}
