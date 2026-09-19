using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

public class ConditionRules
{
    static int Total(int baseValue, StatTarget target, EffectDefinition condition, int value = 0) =>
        Stacking.Resolve(baseValue, target, condition.ModifiersAt(value)).Total;

    static bool Hits(EffectDefinition condition, StatTarget target, int value = 0) =>
        condition.ModifiersAt(value).Any(m => m.AppliesTo(target));

    [Fact]
    public void ClumsyPenalisesDexBasedAttackRolls()
    {
        // The rule is "Dex-based", not a list. A list of AC, Reflex and three skills silently
        // lets a clumsy archer shoot at full accuracy, which was this engine's first bug.
        Assert.Equal(11, Total(13, StatTarget.Attack(AttributeKind.Dexterity), Conditions.Clumsy, 2));
        Assert.False(Hits(Conditions.Clumsy, StatTarget.Attack(AttributeKind.Strength), 2),
            "a Strength-based attack is not Dex-based");
    }

    [Fact]
    public void ClumsyReachesArmorClassReflexAndEveryDexSkill()
    {
        Assert.Equal(-2, Total(0, StatTarget.ArmorClass, Conditions.Clumsy, 2));
        Assert.Equal(-2, Total(0, StatTarget.Reflex, Conditions.Clumsy, 2));
        foreach (var skill in new[] { "Acrobatics", "Stealth", "Thievery" })
        {
            Assert.True(Hits(Conditions.Clumsy, StatTarget.Skill(skill), 1), $"{skill} is Dex-based");
        }

        Assert.False(Hits(Conditions.Clumsy, StatTarget.Will, 1), "Will is Wisdom-based");
        Assert.False(Hits(Conditions.Clumsy, StatTarget.Skill("Athletics"), 1), "Athletics is Strength-based");
    }

    [Fact]
    public void EnfeebledAppliesToStrengthOnlyAndNotToEveryAttack()
    {
        Assert.True(Hits(Conditions.Enfeebled, StatTarget.Attack(AttributeKind.Strength), 2));
        Assert.True(Hits(Conditions.Enfeebled, StatTarget.Damage(AttributeKind.Strength), 2));
        Assert.True(Hits(Conditions.Enfeebled, StatTarget.Skill("Athletics"), 2));

        Assert.False(Hits(Conditions.Enfeebled, StatTarget.Attack(AttributeKind.Dexterity), 2),
            "a rogue's finesse dagger is not Strength-based");
    }

    [Fact]
    public void StupefiedReachesIntelligenceWisdomAndCharismaSkills()
    {
        foreach (var skill in new[] { "Arcana", "Medicine", "Deception", "Occultism", "Nature", "Performance" })
        {
            Assert.True(Hits(Conditions.Stupefied, StatTarget.Skill(skill), 1), $"{skill} is Int, Wis or Cha based");
        }

        Assert.True(Hits(Conditions.Stupefied, StatTarget.Will, 1), "Will is Wisdom-based");
        Assert.True(Hits(Conditions.Stupefied, StatTarget.Perception, 1), "Perception is Wisdom-based");
        Assert.True(Hits(Conditions.Stupefied, StatTarget.SpellDc(AttributeKind.Charisma), 1));

        Assert.False(Hits(Conditions.Stupefied, StatTarget.Fortitude, 1), "Fortitude is Constitution-based");
        Assert.False(Hits(Conditions.Stupefied, StatTarget.Skill("Athletics"), 1), "Athletics is Strength-based");
    }

    [Fact]
    public void DrainedPenalisesConstitutionAndCostsHitPointsPerLevel()
    {
        Assert.Equal(-2, Total(0, StatTarget.Fortitude, Conditions.Drained, 2));
        Assert.False(Hits(Conditions.Drained, StatTarget.Reflex, 2), "Reflex is Dexterity-based");

        // The hit-point loss is not a modifier and no selector can express it.
        Assert.Equal(14, HitPoints.DrainedLoss(drainedValue: 2, level: 7));
    }

    [Fact]
    public void EncumberedIsClumsyOnePlusASpeedPenalty()
    {
        var modifiers = Conditions.Encumbered.ModifiersAt();

        Assert.Equal(2, modifiers.Length);
        Assert.Equal(-1, Stacking.Resolve(0, StatTarget.ArmorClass, modifiers).Total);
        Assert.Equal(15, Stacking.Resolve(25, StatTarget.Speed, modifiers).Total);
        Assert.Equal(ModifierType.Untyped, modifiers.Single(m => m.AppliesTo(StatTarget.Speed)).Type);
    }

    [Fact]
    public void FrightenedAppliesToEveryCheckAndDc()
    {
        foreach (var target in new[]
                 {
                     StatTarget.Fortitude, StatTarget.Reflex, StatTarget.Will, StatTarget.Perception,
                     StatTarget.Attack(AttributeKind.Strength), StatTarget.SpellAttack(AttributeKind.Charisma),
                     StatTarget.Skill("Athletics"), StatTarget.Skill("Stealth"),
                     StatTarget.ClassDc(AttributeKind.Charisma), StatTarget.SpellDc(AttributeKind.Charisma),
                 })
        {
            Assert.True(Hits(Conditions.Frightened, target, 2), $"frightened must reach {target.Kind}");
        }

        Assert.False(Hits(Conditions.Frightened, StatTarget.ArmorClass, 2), "AC is neither a check nor a DC");
        Assert.False(Hits(Conditions.Frightened, StatTarget.Speed, 2), "speed is neither a check nor a DC");
    }

    [Fact]
    public void ProneApplesToEveryAttackWhateverAttributeItUses()
    {
        Assert.Equal(-2, Total(0, StatTarget.Attack(AttributeKind.Strength), Conditions.Prone));
        Assert.Equal(-2, Total(0, StatTarget.Attack(AttributeKind.Dexterity), Conditions.Prone));
        Assert.Equal(ModifierType.Circumstance, Conditions.Prone.ModifiersAt().Single().Type);
    }

    [Fact]
    public void FatiguedHitsArmorClassAndAllThreeSaves()
    {
        foreach (var save in new[] { StatTarget.Fortitude, StatTarget.Reflex, StatTarget.Will })
        {
            Assert.Equal(-1, Total(0, save, Conditions.Fatigued));
        }

        Assert.Equal(-1, Total(0, StatTarget.ArmorClass, Conditions.Fatigued));
        Assert.False(Hits(Conditions.Fatigued, StatTarget.Skill("Stealth")), "fatigued does not touch skills");
    }

    [Fact]
    public void AStatusBonusAndAConditionPenaltyOfTheSameTypeBothApply()
    {
        var anthem = new Modifier("Courageous Anthem", ModifierType.Status, 1,
            [Selector.Exactly(StatKind.Attack), Selector.Exactly(StatKind.Damage)]);

        var result = Stacking.Resolve(0, StatTarget.Attack(AttributeKind.Strength),
            [anthem, .. Conditions.Sickened.ModifiersAt(2)]);

        Assert.Equal(-1, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void AConditionRefusesAValueItDoesNotCarry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Conditions.Prone.ModifiersAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Conditions.Clumsy.ModifiersAt());
    }

    [Fact]
    public void EveryConditionInTheRegistryHasAUniqueKeyAndASourceRef()
    {
        Assert.Equal(Conditions.All.Length, Conditions.All.Select(c => c.Key).Distinct().Count());
        Assert.All(Conditions.All, c => Assert.False(string.IsNullOrWhiteSpace(c.SourceRef), $"{c.Key} has no SourceRef"));
        Assert.All(Conditions.All, c => Assert.False(c.Verified, $"{c.Key} claims to be verified against the book"));
    }
}
