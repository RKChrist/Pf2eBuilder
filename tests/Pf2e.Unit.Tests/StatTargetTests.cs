using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class StatTargetTests
{
    [Fact]
    public void SkillWithoutNameMatchesAnyNamedSkill()
    {
        var everySkill = new StatTarget(StatKind.Skill);

        Assert.True(everySkill.Matches(StatTarget.Skill("Acrobatics")));
        Assert.True(everySkill.Matches(StatTarget.Skill("Lore (Warfare)")));
    }

    [Fact]
    public void NamedSkillMatchesOnlyItself()
    {
        var acrobatics = StatTarget.Skill("Acrobatics");

        Assert.True(acrobatics.Matches(StatTarget.Skill("Acrobatics")));
        Assert.False(acrobatics.Matches(StatTarget.Skill("Stealth")));
    }

    [Fact]
    public void DifferentKindsNeverMatch()
    {
        Assert.False(new StatTarget(StatKind.Reflex).Matches(new StatTarget(StatKind.Will)));
    }

    [Fact]
    public void AllChecksCoversChecksAndDcsButNotArmorClassOrSpeed()
    {
        var kinds = StatTarget.AllChecks.Select(t => t.Kind).ToHashSet();

        Assert.Contains(StatKind.Attack, kinds);
        Assert.Contains(StatKind.Skill, kinds);
        Assert.Contains(StatKind.SpellAttack, kinds);
        Assert.DoesNotContain(StatKind.ArmorClass, kinds);
        Assert.DoesNotContain(StatKind.Speed, kinds);
        Assert.DoesNotContain(StatKind.Damage, kinds);
    }
}
