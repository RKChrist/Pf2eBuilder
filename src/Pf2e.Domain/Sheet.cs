using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// Every number a player reads at the table, each one carrying the breakdown that explains it.
/// </summary>
public sealed record Sheet(
    Breakdown ArmorClass,
    Breakdown Fortitude,
    Breakdown Reflex,
    Breakdown Will,
    Breakdown Perception,
    Breakdown ClassDc,
    int MaxHitPoints,
    int CurrentHitPoints,
    int TemporaryHitPoints,
    int HeroPoints,
    ImmutableArray<ActiveEffect> Effects);

public static class CharacterSheet
{
    public static Sheet Compute(Character build, SessionState session, RuleOptions options = default)
    {
        var modifiers = session.Effects.SelectMany(effect => effect.Modifiers()).ToList();

        // The armour's own item bonus competes with other item bonuses under the stacking rule
        // rather than being added blindly.
        if (build.ArmorItemBonus != 0)
        {
            modifiers.Add(new Modifier(
                build.ArmorName,
                ModifierType.Item,
                build.ArmorItemBonus,
                [Selector.Exactly(StatKind.ArmorClass)]));
        }

        var level = build.Level;

        Breakdown Roll(ProficiencyRank rank, AttributeKind governedBy, StatTarget target) =>
            Stacking.Resolve(
                Proficiency.Bonus(rank, level, options) + build.Attributes.Of(governedBy),
                target,
                modifiers);

        // Drained reduces maximum hit points by its value times level, which no modifier can
        // express. The Constitution modifier stays the build's: drained costs hit points and
        // penalises Con-based checks, it does not lower the attribute.
        var maxHitPoints = Math.Max(1,
            HitPoints.Max(
                build.AncestryHitPoints,
                build.ClassHitPoints,
                level,
                build.Attributes.Constitution,
                build.BonusHitPoints,
                build.BonusHitPointsPerLevel)
            - HitPoints.DrainedLoss(DrainedValue(session), level));

        return new Sheet(
            Domain.ArmorClass.Compute(
                level, build.ArmorRank, build.Attributes.Dexterity, build.ArmorDexCap, modifiers, options),
            Roll(build.Fortitude, AttributeKind.Constitution, StatTarget.Fortitude),
            Roll(build.Reflex, AttributeKind.Dexterity, StatTarget.Reflex),
            Roll(build.Will, AttributeKind.Wisdom, StatTarget.Will),
            Roll(build.Perception, AttributeKind.Wisdom, StatTarget.Perception),
            Stacking.Resolve(
                10 + Proficiency.Bonus(build.ClassDc, level, options) + build.Attributes.Of(build.KeyAttribute),
                StatTarget.ClassDc(build.KeyAttribute),
                modifiers),
            maxHitPoints,
            // Clamping rather than subtracting the drained loss keeps Compute pure: it runs on
            // every read, and subtracting would compound.
            Math.Clamp(session.CurrentHitPoints, 0, maxHitPoints),
            session.TemporaryHitPoints,
            session.HeroPoints,
            session.Effects);
    }

    static int DrainedValue(SessionState session) => session.Effects
        .Where(effect => effect.Source is EffectSource.Seeded seeded
                         && string.Equals(seeded.Key, Conditions.Drained.Key, StringComparison.OrdinalIgnoreCase))
        .Select(effect => effect.ScaledValue)
        .DefaultIfEmpty(0)
        .Max();
}
