using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// The few facts a list row can say about a record without opening it. A table, not a branch:
/// each category names the fields worth a glance in the order a reader wants them, and the first
/// <see cref="Most"/> the record actually carries are the ones shown. A category with no row
/// shows none, which is honest for records whose only facts are their name and source.
/// </summary>
internal static class Highlights
{
    public const int Most = 4;

    static readonly Dictionary<string, string[]> ByCategory = new(StringComparer.Ordinal)
    {
        // Not "feat": every feat carries its own name there, and an echo is not a fact.
        ["feat"] = ["actions", "archetype", "prerequisite", "frequency", "trigger"],
        ["spell"] = ["actions", "tradition", "range_raw", "area_raw", "saving_throw", "target", "duration_raw"],
        ["ritual"] = ["actions", "primary_check", "secondary_casters_raw", "duration_raw"],
        ["weapon"] = ["damage", "weapon_category", "weapon_group", "hands", "price_raw"],
        ["armor"] = ["armor_category", "ac", "dex_cap", "price_raw"],
        ["shield"] = ["ac", "hardness_raw", "hp_raw", "price_raw"],
        ["equipment"] = ["item_subcategory", "price_raw", "bulk_raw", "usage"],
        ["action"] = ["actions", "frequency", "trigger", "requirement"],
        ["skill-general-action"] = ["actions", "trigger", "requirement"],
        ["ancestry"] = ["hp", "size", "speed_raw", "attribute"],
        ["class"] = ["hp", "attribute", "tradition"],
        ["background"] = ["attribute", "skill", "feat"],
        ["deity"] = ["divine_font", "favored_weapon", "domain"],
        ["archetype"] = ["prerequisite"],
        ["class-feature"] = ["class"],
        ["trait"] = ["trait_group"],
        ["skill"] = ["attribute"],
        ["curse"] = ["saving_throw", "usage"],
        ["relic"] = ["prerequisite"],
    };

    public static IReadOnlyList<MechanicField> Of(string category, IReadOnlyList<MechanicField> mechanics)
    {
        if (!ByCategory.TryGetValue(category, out var wanted))
        {
            return [];
        }

        var present = mechanics.ToDictionary(field => field.Key, StringComparer.Ordinal);

        return
        [
            .. wanted
                .Where(present.ContainsKey)
                .Select(key => new MechanicField(key, Cleaned(present[key].Values)))
                .Where(field => field.Values.Count > 0)
                .Take(Most),
        ];
    }

    /// <summary>The seed repeats list members, such as a trait group of Monster, Monster.</summary>
    static IReadOnlyList<string> Cleaned(IReadOnlyList<string> values) =>
        [.. values.Select(value => value.Trim())
                  .Where(value => value.Length > 0)
                  .Distinct(StringComparer.OrdinalIgnoreCase)];
}
