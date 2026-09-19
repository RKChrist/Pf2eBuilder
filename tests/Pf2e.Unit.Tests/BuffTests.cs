using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class BuffTests
{
    public static TheoryData<string> EveryBuffKey => [.. Buffs.All.Select(b => b.Key)];

    [Theory]
    [MemberData(nameof(EveryBuffKey))]
    public void EveryBuffGrantsABonusAndNotAPenalty(string key)
    {
        var buff = Effects.Find(key)!;

        var modifiers = buff.ModifiersAt(buff.HasValue ? 2 : 0);

        Assert.NotEmpty(modifiers);
        Assert.All(modifiers, m => Assert.True(m.IsBonus, $"{buff.Name} gave {m.Value}"));
    }

    [Theory]
    [MemberData(nameof(EveryBuffKey))]
    public void EveryBuffNamesThePageItsNumbersCameFrom(string key)
    {
        var buff = Effects.Find(key)!;

        Assert.StartsWith("Player Core pg. ", buff.SourceRef);
        Assert.False(buff.Verified, $"{buff.Name} claims verification nobody performed");
    }

    [Fact]
    public void ARaisedShieldLiftsArmourClassByTheShieldsOwnBonus()
    {
        var buckler = Assert.Single(Buffs.RaiseAShield.ModifiersAt(1));
        var tower = Assert.Single(Buffs.RaiseAShield.ModifiersAt(4));

        Assert.Equal(1, buckler.Value);
        Assert.Equal(4, tower.Value);
        Assert.Equal(ModifierType.Circumstance, tower.Type);
        Assert.True(tower.AppliesTo(StatTarget.ArmorClass));

        // A raised shield is not a general improvement. It stops an attack and does nothing for
        // a Will save, which is the difference between a shield and a blessing.
        Assert.False(tower.AppliesTo(StatTarget.Will));
        Assert.False(tower.AppliesTo(StatTarget.Perception));
    }

    [Fact]
    public void ARaisedShieldAndCoverDoNotStackBecauseBothAreCircumstanceBonuses()
    {
        var shield = Buffs.RaiseAShield.ModifiersAt(2);
        var cover = Buffs.Cover.ModifiersAt(4);

        var breakdown = Stacking.Resolve(10, StatTarget.ArmorClass, [.. shield, .. cover]);

        Assert.Equal(14, breakdown.Total);
        Assert.Single(breakdown.Applied);
        Assert.Single(breakdown.Suppressed);
        Assert.Equal("Cover +4", breakdown.Applied[0].Source);
    }

    [Fact]
    public void AnAnthemReachesASpellAttackAsWellAsAWeaponOne()
    {
        var anthem = Assert.Single(Buffs.CourageousAnthem.ModifiersAt());

        Assert.True(anthem.AppliesTo(StatTarget.Attack(AttributeKind.Strength)));
        Assert.True(anthem.AppliesTo(StatTarget.SpellAttack(AttributeKind.Charisma)));
        Assert.True(anthem.AppliesTo(StatTarget.Damage(AttributeKind.Strength)));

        // Courageous Anthem is not Rallying Anthem. It does nothing for armour class, and a
        // table that read one chip as the other would stand in the wrong place.
        Assert.False(anthem.AppliesTo(StatTarget.ArmorClass));
    }

    [Fact]
    public void RallyingAnthemGuardsArmourClassAndSavesAndNothingElse()
    {
        var anthem = Assert.Single(Buffs.RallyingAnthem.ModifiersAt());

        Assert.True(anthem.AppliesTo(StatTarget.ArmorClass));
        Assert.True(anthem.AppliesTo(StatTarget.Fortitude));
        Assert.True(anthem.AppliesTo(StatTarget.Reflex));
        Assert.True(anthem.AppliesTo(StatTarget.Will));
        Assert.False(anthem.AppliesTo(StatTarget.Attack(AttributeKind.Strength)));
    }

    [Fact]
    public void HeroismScalesWithTheRankItWasCastAt()
    {
        Assert.Equal(1, Buffs.Heroism.ModifiersAt(1).Single().Value);
        Assert.Equal(3, Buffs.Heroism.ModifiersAt(3).Single().Value);

        var third = Buffs.Heroism.ModifiersAt(3).Single();
        Assert.True(third.AppliesTo(StatTarget.Skill("Stealth")));
        Assert.True(third.AppliesTo(StatTarget.Perception));
        Assert.True(third.AppliesTo(StatTarget.Will));
        Assert.True(third.AppliesTo(StatTarget.Attack(AttributeKind.Dexterity)));
        Assert.False(third.AppliesTo(StatTarget.ArmorClass));
    }

    [Fact]
    public void ABuffAndACursedStatusPenaltyNetOutRatherThanBothApplying()
    {
        // Frightened 2 and a courageous anthem are both status, so the rule takes the best bonus
        // and the worst penalty and applies one of each. This is the commonest thing that happens
        // at a table and the commonest thing a tracker gets wrong.
        Modifier[] modifiers =
        [
            .. Conditions.Frightened.ModifiersAt(2),
            .. Buffs.CourageousAnthem.ModifiersAt(),
        ];

        var attack = Stacking.Resolve(10, StatTarget.Attack(AttributeKind.Strength), modifiers);

        Assert.Equal(9, attack.Total);
        Assert.Equal(2, attack.Applied.Length);
    }

    [Fact]
    public void EveryEffectKeyIsUniqueAcrossConditionsAndBuffs()
    {
        var keys = Effects.All.Select(e => e.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Conditions.All.Length + Buffs.All.Length, keys.Count);
    }

    [Fact]
    public void AConditionIsStillAConditionAndABuffIsMarkedOne()
    {
        Assert.All(Conditions.All, c => Assert.Equal(EffectKind.Condition, c.Kind));
        Assert.All(Buffs.All, b => Assert.Equal(EffectKind.Buff, b.Kind));
    }
}
