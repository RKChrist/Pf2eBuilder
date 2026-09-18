using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

public class ConditionRules
{
    [Fact]
    public void FrightenedAppliesToEveryCheckAndDc()
    {
        var frightened = Conditions.Frightened.AtValue(2);

        foreach (var kind in new[]
                 {
                     StatKind.Fortitude, StatKind.Reflex, StatKind.Will, StatKind.Perception,
                     StatKind.Attack, StatKind.SpellAttack,
                 })
        {
            Assert.True(frightened.AppliesTo(new StatTarget(kind)), $"frightened must apply to {kind}");
        }

        Assert.True(frightened.AppliesTo(StatTarget.Skill("Athletics")), "frightened must apply to every skill");
        Assert.True(frightened.AppliesTo(StatTarget.Skill("Stealth")), "frightened must apply to every skill");
        Assert.False(frightened.AppliesTo(ArmorClass.Target), "AC is neither a check nor a DC");

        Assert.Equal(-2, Stacking.Resolve(0, new StatTarget(StatKind.Will), [frightened]).Total);
    }

    [Fact]
    public void FrightenedLowersClassDcAndSpellDc()
    {
        var frightened = Conditions.Frightened.AtValue(1);

        Assert.Equal(22, Stacking.Resolve(23, new StatTarget(StatKind.ClassDc), [frightened]).Total);
        Assert.Equal(22, Stacking.Resolve(23, new StatTarget(StatKind.SpellDc), [frightened]).Total);
        Assert.True(Conditions.Sickened.AtValue(1).AppliesTo(new StatTarget(StatKind.ClassDc)), "sickened also reads 'all your checks and DCs'");
        Assert.False(Conditions.Stupefied.AtValue(1).AppliesTo(new StatTarget(StatKind.ClassDc)), "stupefied names specific statistics, not every DC");
    }

    [Fact]
    public void ClumsyAppliesToArmorClassAndReflexButNotWill()
    {
        var clumsy = Conditions.Clumsy.AtValue(2);

        Assert.Equal(-2, Stacking.Resolve(0, ArmorClass.Target, [clumsy]).Total);
        Assert.Equal(-2, Stacking.Resolve(0, new StatTarget(StatKind.Reflex), [clumsy]).Total);
        Assert.Equal(0, Stacking.Resolve(0, new StatTarget(StatKind.Will), [clumsy]).Total);
    }

    [Fact]
    public void ClumsyAppliesToDexSkillsButNotOtherSkills()
    {
        var clumsy = Conditions.Clumsy.AtValue(1);

        Assert.True(clumsy.AppliesTo(StatTarget.Skill("Acrobatics")), "Acrobatics is Dex-based");
        Assert.True(clumsy.AppliesTo(StatTarget.Skill("Stealth")), "Stealth is Dex-based");
        Assert.True(clumsy.AppliesTo(StatTarget.Skill("Thievery")), "Thievery is Dex-based");
        Assert.False(clumsy.AppliesTo(StatTarget.Skill("Athletics")), "Athletics is Str-based");
    }

    [Fact]
    public void CircumstanceAndStatusPenaltiesFromConditionsBothApply()
    {
        var result = Stacking.Resolve(20, ArmorClass.Target,
        [
            Conditions.OffGuard.Modifier,
            Conditions.Clumsy.AtValue(1),
        ]);

        Assert.Equal(17, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void TwoStatusConditionsOnTheSameStatisticOnlyApplyTheWorst()
    {
        var result = Stacking.Resolve(20, ArmorClass.Target,
        [
            Conditions.Fatigued.Modifier,
            Conditions.Clumsy.AtValue(2),
        ]);

        Assert.Equal(18, result.Total);
        Assert.Equal("Fatigued", Assert.Single(result.Suppressed).Modifier.Source);
    }

    [Fact]
    public void EncumberedIsClumsyOne()
    {
        var encumbered = Conditions.Encumbered.Modifier;
        var clumsyOne = Conditions.Clumsy.AtValue(1);

        Assert.Equal(clumsyOne.Type, encumbered.Type);
        Assert.Equal(clumsyOne.Value, encumbered.Value);
        Assert.Equal(clumsyOne.Targets, encumbered.Targets);
    }

    [Fact]
    public void EveryConditionInTheRegistryHasAUniqueKeyAndASourceRef()
    {
        Assert.Equal(Conditions.All.Length, Conditions.All.Select(c => c.Key).Distinct().Count());
        Assert.All(Conditions.All, c => Assert.False(string.IsNullOrWhiteSpace(c.SourceRef), $"{c.Key} has no SourceRef"));
    }
}
