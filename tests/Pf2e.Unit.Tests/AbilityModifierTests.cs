using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class AbilityModifierTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(3, -4)]
    [InlineData(7, -2)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(19, 4)]
    [InlineData(20, 5)]
    [InlineData(21, 5)]
    public void ModifierIsFloorOfScoreMinusTenOverTwo(int score, int expected)
    {
        Assert.Equal(expected, Ability.Modifier(score));
    }
}
