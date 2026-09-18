using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class HitPointsTests
{
    [Fact]
    public void BonusHpPerLevelIsMultipliedByLevel()
    {
        var without = HitPoints.Max(ancestryHp: 8, classHp: 10, level: 5, conModifier: 1);
        var with = HitPoints.Max(ancestryHp: 8, classHp: 10, level: 5, conModifier: 1, bonusHpPerLevel: 2);

        Assert.Equal(10, with - without);
    }

    [Fact]
    public void FlatBonusHpIsAddedOnce()
    {
        var without = HitPoints.Max(ancestryHp: 8, classHp: 10, level: 5, conModifier: 1);
        var with = HitPoints.Max(ancestryHp: 8, classHp: 10, level: 5, conModifier: 1, bonusHp: 3);

        Assert.Equal(3, with - without);
    }

    [Fact]
    public void NegativeConModifierReducesHpEveryLevel()
    {
        Assert.Equal(8 + 5 * (10 - 1), HitPoints.Max(ancestryHp: 8, classHp: 10, level: 5, conModifier: -1));
    }
}
