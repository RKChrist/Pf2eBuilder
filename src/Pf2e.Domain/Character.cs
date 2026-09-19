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
    int BonusHitPointsPerLevel);
