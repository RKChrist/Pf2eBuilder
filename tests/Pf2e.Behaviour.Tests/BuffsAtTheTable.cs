using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// The buffs a table applies to itself, computed on the sheet the session actually holds. These
/// exist because the engine shipped with fourteen conditions and no bonuses at all, so a bard
/// could sing and the tracker showed nothing.
/// </summary>
public class BuffsAtTheTable
{
    static ActiveEffect Buff(string key, int value = 0) =>
        new(Guid.NewGuid(), Effects.Find(key)!.Name, value, new EffectSource.Seeded(key));

    [Fact]
    public void ARaisedShieldMovesArmourClassAndNothingElse()
    {
        var bare = Gnibbo.SheetWith();
        var raised = Gnibbo.SheetWith(Buff("raise-a-shield", 2));

        Assert.Equal(bare.ArmorClass.Total + 2, raised.ArmorClass.Total);
        Assert.Equal(bare.Will.Total, raised.Will.Total);
        Assert.Equal(bare.Perception.Total, raised.Perception.Total);
    }

    [Fact]
    public void ARaisedShieldAndCoverGiveTheBetterOfTheTwoRatherThanBoth()
    {
        var bare = Gnibbo.SheetWith();

        var both = Gnibbo.SheetWith(Buff("raise-a-shield", 2), Buff("cover", 4));

        Assert.Equal(bare.ArmorClass.Total + 4, both.ArmorClass.Total);
        Assert.Single(both.ArmorClass.Suppressed);
    }

    [Fact]
    public void AnAnthemChangesNothingOnThisSheetYetBecauseTheSheetHasNoAttackRoll()
    {
        // Courageous Anthem and bless both land on attack rolls and damage, and this sheet
        // computes armour class, three saves, Perception and a class DC. So the modifier is
        // real, the stacking rule sees it, and the player sees nothing. That is the gap, and
        // this test exists to fail the moment attacks join the sheet, which is when it should
        // be rewritten into the assertion it wants to be.
        var bare = Gnibbo.SheetWith();
        var sung = Gnibbo.SheetWith(Buff("courageous-anthem"));

        Assert.Equal(bare.ArmorClass.Total, sung.ArmorClass.Total);
        Assert.Equal(bare.Will.Total, sung.Will.Total);
        Assert.Equal(bare.Perception.Total, sung.Perception.Total);
        Assert.Equal(bare.ClassDc.Total, sung.ClassDc.Total);

        // It is applied all the same, and the effect is on the character where a screen can
        // show it as a chip.
        Assert.Single(sung.Effects);
    }

    [Fact]
    public void RallyingAnthemGuardsTheArmourClassAFrightenedBardJustLost()
    {
        var bare = Gnibbo.SheetWith();

        // Frightened lowers armour class, because armour class is a DC. The anthem gives a status
        // bonus of the same size back, and the two net to nothing.
        var scared = Gnibbo.SheetWith(Gnibbo.Condition("frightened", 1));
        var rallied = Gnibbo.SheetWith(Gnibbo.Condition("frightened", 1), Buff("rallying-anthem"));

        Assert.Equal(bare.ArmorClass.Total - 1, scared.ArmorClass.Total);
        Assert.Equal(bare.ArmorClass.Total, rallied.ArmorClass.Total);
    }

    [Fact]
    public void HeroismLiftsEveryNumberItSaysItDoes()
    {
        var bare = Gnibbo.SheetWith();
        var heroic = Gnibbo.SheetWith(Buff("heroism", 2));

        Assert.Equal(bare.Perception.Total + 2, heroic.Perception.Total);
        Assert.Equal(bare.Will.Total + 2, heroic.Will.Total);
        Assert.Equal(bare.Fortitude.Total + 2, heroic.Fortitude.Total);
        Assert.Equal(bare.ArmorClass.Total, heroic.ArmorClass.Total);
    }
}
