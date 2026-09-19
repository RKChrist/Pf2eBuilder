using System.Globalization;
using Pf2e.Components;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Catalog;

/// <summary>One phrase of a glance line. An action cost is drawn with the kit's glyph rather
/// than spelled out, because that is how the rules print it.</summary>
public sealed record GlancePart(string Text, ActionCostKind? Cost = null);

/// <summary>
/// What a row says about a record in one line, built from the facts the server chose as its
/// highlights. The server decides which facts; this decides how each reads. Keyed by field, not
/// by category, so a trigger reads the same on a feat as on an action.
/// </summary>
public static class Glance
{
    public const string Separator = " · ";

    static readonly Dictionary<string, string> Phrasing = new(StringComparer.Ordinal)
    {
        ["archetype"] = "{0} archetype",
        ["prerequisite"] = "Prerequisites: {0}",
        ["trigger"] = "Trigger: {0}",
        ["requirement"] = "Requirements: {0}",
        ["range_raw"] = "Range {0}",
        ["area_raw"] = "Area {0}",
        ["target"] = "Targets {0}",
        ["saving_throw"] = "{0} save",
        ["duration_raw"] = "Lasts {0}",
        ["secondary_casters_raw"] = "Secondary casters: {0}",
        ["hands"] = "Hands: {0}",
        ["ac"] = "+{0} AC",
        ["dex_cap"] = "Dex cap +{0}",
        ["hardness_raw"] = "Hardness {0}",
        ["hp_raw"] = "HP {0}",
        ["bulk_raw"] = "Bulk {0}",
        ["hp"] = "{0} HP",
        ["speed_raw"] = "Speed {0}",
        ["attribute"] = "Attributes: {0}",
        ["skill"] = "Skills: {0}",
        ["feat"] = "Feat: {0}",
        ["divine_font"] = "Font: {0}",
        ["favored_weapon"] = "Favored weapon: {0}",
        ["domain"] = "Domains: {0}",
        ["trait_group"] = "Group: {0}",
    };

    static readonly Dictionary<string, ActionCostKind> Costs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Single Action"] = ActionCostKind.One,
        ["Two Actions"] = ActionCostKind.Two,
        ["Three Actions"] = ActionCostKind.Three,
        ["Reaction"] = ActionCostKind.Reaction,
        ["Free Action"] = ActionCostKind.Free,
    };

    /// <summary>The seed spells out ranges of cost; a glance wants them short.</summary>
    static readonly (string Printed, string Short)[] CostWords =
    [
        ("Single Action", "1"),
        ("One Action", "1"),
        ("Two Actions", "2"),
        ("Three Actions", "3"),
        ("more Actions", "more actions"),
    ];

    public static IReadOnlyList<GlancePart> Of(RuleSummary rule) => [.. rule.Highlights.Select(Part)];

    public static GlancePart Part(MechanicField field)
    {
        var value = string.Join(", ", field.Values);

        if (field.Key == "actions")
        {
            return Costs.TryGetValue(value, out var cost)
                ? new GlancePart(cost.AriaLabel(), cost)
                : new GlancePart(Shortened(value));
        }

        return new GlancePart(Phrasing.TryGetValue(field.Key, out var phrase)
            ? string.Format(CultureInfo.InvariantCulture, phrase, value)
            : value);
    }

    /// <summary>"Single Action to Three Actions" becomes "1 to 3 actions"; a cast time such as
    /// "1 minute" is left alone.</summary>
    static string Shortened(string printed)
    {
        var text = CostWords.Aggregate(printed, (current, word) =>
            current.Replace(word.Printed, word.Short, StringComparison.OrdinalIgnoreCase));

        return text == printed || text.EndsWith("actions", StringComparison.Ordinal) ? text : $"{text} actions";
    }

    public static string Text(RuleSummary rule) => string.Join(Separator, Of(rule).Select(part => part.Text));
}

public static class Numbers
{
    public static string Grouped(int count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
