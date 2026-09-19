using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>
/// The stored form of one character, holding the build columns and the session columns side by
/// side.
/// <para>Only <see cref="From"/> and <see cref="Apply"/> write a build column, which is what
/// makes "a re-import replaces the build and preserves the session" a thing the compiler
/// enforces rather than a thing a reviewer has to notice. The session columns are settable
/// because the hit-point handler legitimately writes them.</para>
/// </summary>
public sealed class TrackedCharacter
{
    private TrackedCharacter()
    {
    }

    public Guid Id { get; private set; }
    public Guid TableId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public int Level { get; private set; }
    public string ClassName { get; private set; } = string.Empty;
    public string AncestryName { get; private set; } = string.Empty;
    public AttributeKind KeyAttribute { get; private set; }
    public int Strength { get; private set; }
    public int Dexterity { get; private set; }
    public int Constitution { get; private set; }
    public int Intelligence { get; private set; }
    public int Wisdom { get; private set; }
    public int Charisma { get; private set; }
    public ProficiencyRank Fortitude { get; private set; }
    public ProficiencyRank Reflex { get; private set; }
    public ProficiencyRank Will { get; private set; }
    public ProficiencyRank Perception { get; private set; }
    public ProficiencyRank ClassDc { get; private set; }
    public ProficiencyRank ArmorRank { get; private set; }
    public string ArmorName { get; private set; } = string.Empty;
    public int ArmorItemBonus { get; private set; }
    public int? ArmorDexCap { get; private set; }
    public int AncestryHitPoints { get; private set; }
    public int ClassHitPoints { get; private set; }
    public int BonusHitPoints { get; private set; }
    public int BonusHitPointsPerLevel { get; private set; }

    // Build data that is a list rather than a column. It is read whole and replaced whole on
    // re-import and nothing queries inside it, so it is stored as one JSON value per list and
    // not as two more tables.
    public ImmutableArray<SkillProficiency> Skills { get; private set; } = [];
    public ImmutableArray<WeaponAttack> Weapons { get; private set; } = [];
    public Spellcasting? Spellcasting { get; private set; }

    public int CurrentHitPoints { get; set; }
    public int TemporaryHitPoints { get; set; }
    public int HeroPoints { get; set; }

    public List<TrackedEffect> Effects { get; } = [];

    public static TrackedCharacter From(Guid tableId, Character build, SessionState session)
    {
        var character = new TrackedCharacter
        {
            Id = Guid.NewGuid(),
            TableId = tableId,
            CurrentHitPoints = session.CurrentHitPoints,
            TemporaryHitPoints = session.TemporaryHitPoints,
            HeroPoints = session.HeroPoints,
        };

        character.Apply(build);
        character.Effects.AddRange(session.Effects.Select(e => TrackedEffect.From(character.Id, e)));
        return character;
    }

    public Character ToBuild() => new(
        Name,
        Level,
        ClassName,
        AncestryName,
        KeyAttribute,
        new AttributeModifiers(Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma),
        Fortitude,
        Reflex,
        Will,
        Perception,
        ClassDc,
        ArmorRank,
        ArmorName,
        ArmorItemBonus,
        ArmorDexCap,
        AncestryHitPoints,
        ClassHitPoints,
        BonusHitPoints,
        BonusHitPointsPerLevel,
        Skills,
        Weapons,
        Spellcasting);

    public SessionState ToSession() => new(
        CurrentHitPoints,
        TemporaryHitPoints,
        HeroPoints,
        [.. Effects.Select(effect => effect.ToActive())]);

    /// <summary>
    /// The only place that expresses "the build is replaced, the session is preserved". A player
    /// who levels up mid-session must not get their hit points reset to full.
    /// </summary>
    public void Apply(Character build)
    {
        Name = build.Name;
        Level = build.Level;
        ClassName = build.ClassName;
        AncestryName = build.AncestryName;
        KeyAttribute = build.KeyAttribute;
        Strength = build.Attributes.Strength;
        Dexterity = build.Attributes.Dexterity;
        Constitution = build.Attributes.Constitution;
        Intelligence = build.Attributes.Intelligence;
        Wisdom = build.Attributes.Wisdom;
        Charisma = build.Attributes.Charisma;
        Fortitude = build.Fortitude;
        Reflex = build.Reflex;
        Will = build.Will;
        Perception = build.Perception;
        ClassDc = build.ClassDc;
        ArmorRank = build.ArmorRank;
        ArmorName = build.ArmorName;
        ArmorItemBonus = build.ArmorItemBonus;
        ArmorDexCap = build.ArmorDexCap;
        AncestryHitPoints = build.AncestryHitPoints;
        ClassHitPoints = build.ClassHitPoints;
        BonusHitPoints = build.BonusHitPoints;
        BonusHitPointsPerLevel = build.BonusHitPointsPerLevel;
        Skills = build.Skills;
        Weapons = build.Weapons;
        Spellcasting = build.Spellcasting;
    }
}
