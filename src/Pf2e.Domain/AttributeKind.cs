namespace Pf2e.Domain;

/// <summary>
/// Named <c>AttributeKind</c> rather than <c>Attribute</c> so it does not shadow
/// <see cref="System.Attribute"/> inside this namespace.
/// </summary>
public enum AttributeKind
{
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma,
}

/// <summary>
/// Which attribute governs each skill. This table is what makes "every Dex-based check"
/// computable, and conditions like clumsy and stupefied are defined in exactly those terms.
/// </summary>
public static class Skills
{
    public static readonly IReadOnlyDictionary<string, AttributeKind> GovernedBy =
        new Dictionary<string, AttributeKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["Acrobatics"] = AttributeKind.Dexterity,
            ["Arcana"] = AttributeKind.Intelligence,
            ["Athletics"] = AttributeKind.Strength,
            ["Crafting"] = AttributeKind.Intelligence,
            ["Deception"] = AttributeKind.Charisma,
            ["Diplomacy"] = AttributeKind.Charisma,
            ["Intimidation"] = AttributeKind.Charisma,
            ["Medicine"] = AttributeKind.Wisdom,
            ["Nature"] = AttributeKind.Wisdom,
            ["Occultism"] = AttributeKind.Intelligence,
            ["Performance"] = AttributeKind.Charisma,
            ["Religion"] = AttributeKind.Wisdom,
            ["Society"] = AttributeKind.Intelligence,
            ["Stealth"] = AttributeKind.Dexterity,
            ["Survival"] = AttributeKind.Wisdom,
            ["Thievery"] = AttributeKind.Dexterity,
        };

    /// <summary>Lore skills are Intelligence-based whatever their subject.</summary>
    public static AttributeKind? For(string skillName) =>
        GovernedBy.TryGetValue(skillName, out var attribute) ? attribute
        : skillName.Contains("Lore", StringComparison.OrdinalIgnoreCase) ? AttributeKind.Intelligence
        : null;
}
