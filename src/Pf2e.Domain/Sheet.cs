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
    ImmutableArray<ActiveEffect> Effects,
    ImmutableArray<NamedBreakdown> Skills = default,
    ImmutableArray<NamedBreakdown> Attacks = default,
    Breakdown? SpellAttack = null,
    Breakdown? SpellDc = null)
{
    public ImmutableArray<NamedBreakdown> Skills { get; init; } = Skills.IsDefault ? [] : Skills;

    public ImmutableArray<NamedBreakdown> Attacks { get; init; } = Attacks.IsDefault ? [] : Attacks;
}

/// <summary>A computed number that has a name of its own rather than a slot on the sheet: one
/// skill, one weapon. The name is the player's, so "Warfare Lore" reads as it is written.
/// <see cref="Rank"/> is the skill's proficiency and null for a weapon, whose rank is folded
/// into the export's own total and not separable from it.</summary>
public sealed record NamedBreakdown(string Name, Breakdown Value, ProficiencyRank? Rank = null);

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
            session.Effects,
            [.. build.Skills
                .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .Select(skill => new NamedBreakdown(
                    skill.Name,
                    Roll(skill.Rank, Skills.For(skill.Name) ?? AttributeKind.Intelligence, StatTarget.Skill(skill.Name)),
                    skill.Rank))],
            // A weapon's bonus is the export's own total, so the base is taken whole and only
            // the session's modifiers are stacked onto it.
            [.. build.Weapons.Select(weapon => new NamedBreakdown(
                weapon.Display,
                Stacking.Resolve(weapon.Bonus, StatTarget.Attack(weapon.GovernedBy), modifiers)))],
            build.Spellcasting is { } casting
                ? Roll(casting.Rank, casting.Attribute, StatTarget.SpellAttack(casting.Attribute))
                : null,
            build.Spellcasting is { } dc
                ? Stacking.Resolve(
                    10 + Proficiency.Bonus(dc.Rank, level, options) + build.Attributes.Of(dc.Attribute),
                    StatTarget.SpellDc(dc.Attribute),
                    modifiers)
                : null);
    }

    static int DrainedValue(SessionState session) => session.Effects
        .Where(effect => effect.Source is EffectSource.Seeded seeded
                         && string.Equals(seeded.Key, Conditions.Drained.Key, StringComparison.OrdinalIgnoreCase))
        .Select(effect => effect.ScaledValue)
        .DefaultIfEmpty(0)
        .Max();
}
