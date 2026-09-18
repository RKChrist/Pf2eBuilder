namespace Pf2e.Domain;

public static class ArmorClass
{
    public static readonly StatTarget Target = new(StatKind.ArmorClass);

    /// <summary>
    /// 10 + armour proficiency + Dex (capped by the armour) + stacked modifiers. The armour's own
    /// item bonus is passed as an item <see cref="Modifier"/> so it competes with other item
    /// bonuses under the stacking rule instead of being added blindly.
    /// </summary>
    public static Breakdown Compute(
        int level,
        ProficiencyRank armorRank,
        int dexModifier,
        int? armorDexCap,
        IEnumerable<Modifier> modifiers,
        RuleOptions options = default)
    {
        var cappedDex = armorDexCap is int cap ? Math.Min(dexModifier, cap) : dexModifier;
        var baseValue = 10 + Proficiency.Bonus(armorRank, level, options) + cappedDex;
        return Stacking.Resolve(baseValue, Target, modifiers);
    }
}
