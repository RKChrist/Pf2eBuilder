using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

public class AbilityRules
{
    [Fact]
    public void AbilityModifierRoundsDownForOddLowScores()
    {
        Assert.Equal(-2, Ability.Modifier(7));
        Assert.Equal(-1, Ability.Modifier(9));
    }

    [Fact]
    public void AbilityModifierIsHalfOfScoreAboveTen()
    {
        Assert.Equal(0, Ability.Modifier(10));
        Assert.Equal(2, Ability.Modifier(14));
        Assert.Equal(3, Ability.Modifier(16));
    }
}
