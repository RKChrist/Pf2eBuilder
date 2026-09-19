namespace Pf2e.Domain;

public enum StatKind
{
    ArmorClass,
    Fortitude,
    Reflex,
    Will,
    Perception,
    Attack,
    Damage,
    Skill,
    ClassDc,
    SpellAttack,
    SpellDc,
    Speed,
}

/// <summary>
/// One statistic being computed. It carries the attribute that governs it, because Pathfinder
/// writes conditions in terms of derived categories: clumsy is every Dex-based statistic, not a
/// list of five. A list goes stale the moment a new Dex-based skill appears. A category does not.
/// </summary>
public readonly record struct StatTarget(
    StatKind Kind,
    string? SkillName = null,
    AttributeKind? GovernedBy = null)
{
    public static StatTarget ArmorClass => new(StatKind.ArmorClass, GovernedBy: AttributeKind.Dexterity);
    public static StatTarget Fortitude => new(StatKind.Fortitude, GovernedBy: AttributeKind.Constitution);
    public static StatTarget Reflex => new(StatKind.Reflex, GovernedBy: AttributeKind.Dexterity);
    public static StatTarget Will => new(StatKind.Will, GovernedBy: AttributeKind.Wisdom);
    public static StatTarget Perception => new(StatKind.Perception, GovernedBy: AttributeKind.Wisdom);
    public static StatTarget Speed => new(StatKind.Speed);

    public static StatTarget Skill(string skillName) =>
        new(StatKind.Skill, skillName, Skills.For(skillName));

    /// <summary>An attack roll, governed by whichever attribute the weapon uses.</summary>
    public static StatTarget Attack(AttributeKind governedBy) =>
        new(StatKind.Attack, GovernedBy: governedBy);

    /// <summary>Damage. Only Strength-based damage is Strength-governed.</summary>
    public static StatTarget Damage(AttributeKind? governedBy = null) =>
        new(StatKind.Damage, GovernedBy: governedBy);

    public static StatTarget SpellAttack(AttributeKind castingAttribute) =>
        new(StatKind.SpellAttack, GovernedBy: castingAttribute);

    public static StatTarget SpellDc(AttributeKind castingAttribute) =>
        new(StatKind.SpellDc, GovernedBy: castingAttribute);

    public static StatTarget ClassDc(AttributeKind keyAttribute) =>
        new(StatKind.ClassDc, GovernedBy: keyAttribute);

    /// <summary>A roll you make. Armour class, damage and speed are not.</summary>
    public bool IsCheck => Kind
        is StatKind.Fortitude or StatKind.Reflex or StatKind.Will or StatKind.Perception
        or StatKind.Attack or StatKind.Skill or StatKind.SpellAttack;

    /// <summary>
    /// Armour class is a DC. Player Core page 10: a creature's armour class "serves as the
    /// Difficulty Class for hitting" it. So a condition whose text reads "all your checks and
    /// DCs", which is frightened and sickened, lowers armour class too. This engine excluded it
    /// and a test asserted the exclusion, which meant a frightened creature was harder to hit
    /// than the rules say.
    /// </summary>
    public bool IsDc => Kind is StatKind.ClassDc or StatKind.SpellDc or StatKind.ArmorClass;

    public bool IsSavingThrow => Kind is StatKind.Fortitude or StatKind.Reflex or StatKind.Will;
}
