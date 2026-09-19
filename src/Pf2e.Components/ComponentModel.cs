namespace Pf2e.Components;

public enum ButtonVariant { Primary, Secondary, Quiet, Destructive }

public enum ButtonType { Button, Submit, Reset }

public enum ActionCostKind { One, Two, Three, Reaction, Free }

public enum Rarity { Common, Uncommon, Rare, Unique }

public enum ProficiencyRank { Untrained, Trained, Expert, Master, Legendary }

public enum ModifierKind { Status, Circumstance, Item, Proficiency, Untyped }

public sealed record ChoiceOption<TValue>(TValue Value, string Text, bool Disabled = false);

public static class ComponentModel
{
    public static string CssSuffix(this ButtonVariant variant) => variant switch
    {
        ButtonVariant.Primary => "primary",
        ButtonVariant.Secondary => "secondary",
        ButtonVariant.Quiet => "quiet",
        ButtonVariant.Destructive => "destructive",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };

    public static string HtmlValue(this ButtonType type) => type switch
    {
        ButtonType.Button => "button",
        ButtonType.Submit => "submit",
        ButtonType.Reset => "reset",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static int GlyphCount(this ActionCostKind cost) => cost switch
    {
        ActionCostKind.One => 1,
        ActionCostKind.Two => 2,
        ActionCostKind.Three => 3,
        ActionCostKind.Reaction => 1,
        ActionCostKind.Free => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null),
    };

    public static string? CssModifier(this ActionCostKind cost) => cost switch
    {
        ActionCostKind.One => null,
        ActionCostKind.Two => null,
        ActionCostKind.Three => null,
        ActionCostKind.Reaction => "reaction",
        ActionCostKind.Free => "free",
        _ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null),
    };

    public static string AriaLabel(this ActionCostKind cost) => cost switch
    {
        ActionCostKind.One => "1 action",
        ActionCostKind.Two => "2 actions",
        ActionCostKind.Three => "3 actions",
        ActionCostKind.Reaction => "Reaction",
        ActionCostKind.Free => "Free action",
        _ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null),
    };

    public static string CssSuffix(this Rarity rarity) => rarity switch
    {
        Rarity.Common => "common",
        Rarity.Uncommon => "uncommon",
        Rarity.Rare => "rare",
        Rarity.Unique => "unique",
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
    };

    public static string DisplayText(this Rarity rarity) => rarity switch
    {
        Rarity.Common => "Common",
        Rarity.Uncommon => "Uncommon",
        Rarity.Rare => "Rare",
        Rarity.Unique => "Unique",
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
    };

    public static string DisplayText(this ProficiencyRank rank) => rank switch
    {
        ProficiencyRank.Untrained => "Untrained",
        ProficiencyRank.Trained => "Trained",
        ProficiencyRank.Expert => "Expert",
        ProficiencyRank.Master => "Master",
        ProficiencyRank.Legendary => "Legendary",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null),
    };

    /// <summary>
    /// Steps of training, so Untrained fills none. Four pips rather than five, because five
    /// ranks on five pips forces Untrained to light one, and untrained is the absence of
    /// training rather than the first degree of it. This also matches the arithmetic: the
    /// proficiency bonus is twice the number of filled pips.
    /// </summary>
    public static int FilledPips(this ProficiencyRank rank) => (int)rank;

    public static string DisplayText(this ModifierKind kind) => kind switch
    {
        ModifierKind.Status => "Status",
        ModifierKind.Circumstance => "Circumstance",
        ModifierKind.Item => "Item",
        ModifierKind.Proficiency => "Proficiency",
        ModifierKind.Untyped => "Untyped",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
