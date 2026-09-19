using System.Collections.Immutable;
namespace Pf2e.Domain;

/// <summary>
/// Six named fields rather than a dictionary, so a missing attribute is not a state the type can
/// hold and equality compares by value.
/// </summary>
public readonly record struct AttributeModifiers(
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma)
{
    public int Of(AttributeKind kind) => kind switch
    {
        AttributeKind.Strength => Strength,
        AttributeKind.Dexterity => Dexterity,
        AttributeKind.Constitution => Constitution,
        AttributeKind.Intelligence => Intelligence,
        AttributeKind.Wisdom => Wisdom,
        AttributeKind.Charisma => Charisma,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an attribute."),
    };
}

/// <summary>One skill and the rank the build has in it. Untrained skills are here too, because
/// an untrained Athletics is still a roll somebody makes and a tracker that hides it makes them
/// do the arithmetic themselves.</summary>
public readonly record struct SkillProficiency(string Name, ProficiencyRank Rank);

/// <summary>
/// One weapon's attack roll.
/// <para><see cref="Bonus"/> is the export's own total rather than a number recomputed here.
/// A class grants proficiency in named weapons that no field of the export states: this bard is
/// untrained in martial weapons and expert with a rapier, and recomputing from the proficiency
/// table gives +4 where the true number is +15. The export knows; we do not.</para>
/// <para><see cref="GovernedBy"/> is resolved at import from the seeded weapon's own traits,
/// because a selector has to know it: clumsy has to reach a finesse rapier and must not reach a
/// greatsword.</para>
/// </summary>
public readonly record struct WeaponAttack(string Name, string Display, int Bonus, AttributeKind GovernedBy);

/// <summary>
/// One thing off the character's own sheet that the ruleset also has a record of.
/// <para><see cref="RuleId"/> is the seeded record, resolved at import by name, and null for
/// something the ruleset does not hold: homebrew, a typo, or a feat newer than the snapshot. A
/// null id is why this carries the name too. The reference screen shows the name either way and
/// only offers to open the ones it can.</para>
/// <para><see cref="Kind"/> is the export's own word for it, such as "Class Feat" or "Ancestry
/// Feat", or the spell's rank written as "Cantrip" or "Rank 3".</para>
/// </summary>
public readonly record struct SheetEntry(string Name, string Kind, int Level, string? RuleId);

/// <summary>The tradition and rank a spell attack and spell DC are built from. Null on a
/// character who casts nothing, which is most fighters.</summary>
public readonly record struct Spellcasting(string Tradition, ProficiencyRank Rank, AttributeKind Attribute);

/// <summary>
/// The layer that changes at level-up. It is replaced wholesale on re-import, which is why it
/// holds nothing a player changes during play.
/// </summary>
public sealed record Character(
    string Name,
    int Level,
    string ClassName,
    string AncestryName,
    AttributeKind KeyAttribute,
    AttributeModifiers Attributes,
    ProficiencyRank Fortitude,
    ProficiencyRank Reflex,
    ProficiencyRank Will,
    ProficiencyRank Perception,
    ProficiencyRank ClassDc,
    ProficiencyRank ArmorRank,
    string ArmorName,
    int ArmorItemBonus,
    int? ArmorDexCap,
    int AncestryHitPoints,
    int ClassHitPoints,
    int BonusHitPoints,
    int BonusHitPointsPerLevel,
    ImmutableArray<SkillProficiency> Skills = default,
    ImmutableArray<WeaponAttack> Weapons = default,
    Spellcasting? Spellcasting = null,
    ImmutableArray<SheetEntry> Feats = default,
    ImmutableArray<SheetEntry> Spells = default)
{
    // A default ImmutableArray is not an empty one and throws on enumeration. These arrive from
    // deserialization and from a positional default, where absent is exactly that.
    public ImmutableArray<SkillProficiency> Skills { get; init; } = Skills.IsDefault ? [] : Skills;

    public ImmutableArray<WeaponAttack> Weapons { get; init; } = Weapons.IsDefault ? [] : Weapons;

    public ImmutableArray<SheetEntry> Feats { get; init; } = Feats.IsDefault ? [] : Feats;

    public ImmutableArray<SheetEntry> Spells { get; init; } = Spells.IsDefault ? [] : Spells;

    /// <summary>
    /// The maximum hit points the export stated, when it stated one instead of the parts.
    /// <para>A Pathbuilder export gives ancestry hit points, class hit points and the bonuses,
    /// and the engine adds them up. A Wanderer's Guide export gives the total and no split, so
    /// there is nothing to add up and nothing to be gained by guessing a split that would
    /// multiply back to the same number.</para>
    /// <para>Drained still comes off it, because drained is a session effect and this is the
    /// build's number.</para>
    /// </summary>
    public int? StatedMaxHitPoints { get; init; }
}
