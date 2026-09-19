using Pf2e.Components;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Catalog;

/// <summary>What the seed's free text means to the component kit's closed sets.</summary>
public static class RuleDisplay
{
    public static Rarity RarityOf(string? published) => published?.ToLowerInvariant() switch
    {
        "uncommon" => Rarity.Uncommon,
        "rare" => Rarity.Rare,
        "unique" => Rarity.Unique,
        _ => Rarity.Common,
    };

    /// <summary>Archives of Nethys publishes rarity as a trait as well as a field, and the
    /// rarity badge already draws it.</summary>
    public static IEnumerable<string> TraitsBeside(RuleSummary summary) =>
        summary.Traits.Where(trait =>
            !string.Equals(trait, summary.Rarity, StringComparison.OrdinalIgnoreCase));

    public static ModifierKind KindOf(string published) => published switch
    {
        nameof(ModifierKind.Circumstance) => ModifierKind.Circumstance,
        nameof(ModifierKind.Item) => ModifierKind.Item,
        nameof(ModifierKind.Status) => ModifierKind.Status,
        nameof(ModifierKind.Proficiency) => ModifierKind.Proficiency,
        _ => ModifierKind.Untyped,
    };
}
