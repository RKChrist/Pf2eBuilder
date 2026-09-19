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
    public void AnAnthemAndAFrighteningMonsterBothReachTheSameAttackRoll()
    {
        var bare = Gnibbo.SheetWith();
        var sung = Gnibbo.SheetWith(Buff("courageous-anthem"));
        var sungAndScared = Gnibbo.SheetWith(Buff("courageous-anthem"), Gnibbo.Condition("frightened", 2));

        static int Rapier(Sheet sheet) =>
            sheet.Attacks.Single(attack => attack.Name == "+1 Striking Rapier").Value.Total;

        Assert.Equal(2, bare.Attacks.Length);
        Assert.Equal(Rapier(bare) + 1, Rapier(sung));

        // Both are status, so the rule takes the best bonus and the worst penalty and applies
        // one of each rather than summing them: +1 and -2 leave the roll one worse than bare.
        Assert.Equal(Rapier(bare) - 1, Rapier(sungAndScared));
    }

    [Fact]
    public void AnAnthemReachesASpellAttackTheSameWayItReachesAWeaponOne()
    {
        var bare = Gnibbo.SheetWith();
        var sung = Gnibbo.SheetWith(Buff("courageous-anthem"));

        Assert.Equal(bare.SpellAttack!.Total + 1, sung.SpellAttack!.Total);

        // A spell DC is a DC and not an attack roll, so the anthem leaves it alone.
        Assert.Equal(bare.SpellDc!.Total, sung.SpellDc!.Total);
    }

    [Fact]
    public void HeroismReachesEverySkillAndClumsyReachesOnlyTheDexterousOnes()
    {
        var bare = Gnibbo.SheetWith();
        var heroic = Gnibbo.SheetWith(Buff("heroism", 2));
        var clumsy = Gnibbo.SheetWith(Gnibbo.Condition("clumsy", 1));

        static int Of(Sheet sheet, string name) =>
            sheet.Skills.Single(skill => skill.Name == name).Value.Total;

        Assert.All(bare.Skills, skill => Assert.Equal(Of(bare, skill.Name) + 2, Of(heroic, skill.Name)));

        // Clumsy is every Dexterity-based statistic, which is Stealth and not Performance.
        Assert.Equal(Of(bare, "Stealth") - 1, Of(clumsy, "Stealth"));
        Assert.Equal(Of(bare, "Performance"), Of(clumsy, "Performance"));
    }

    [Fact]
    public void AnUntrainedSkillIsTheAttributeAloneAndATrainedOneAddsLevelAndRank()
    {
        var sheet = Gnibbo.SheetWith();

        // Athletics is untrained and Strength 10, so it is exactly zero. Stealth is expert at
        // level 7 with Dexterity 16: 7 + 4 + 3.
        Assert.Equal(0, sheet.Skills.Single(skill => skill.Name == "Athletics").Value.Total);
        Assert.Equal(14, sheet.Skills.Single(skill => skill.Name == "Stealth").Value.Total);

        // A lore is governed by Intelligence like any other Lore, and this one is trained.
        Assert.Equal(7 + 2 + 1, sheet.Skills.Single(skill => skill.Name == "Warfare Lore").Value.Total);
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
