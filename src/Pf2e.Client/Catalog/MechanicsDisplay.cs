using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Catalog;

public sealed record MechanicRow(string Label, IReadOnlyList<string> Values);

/// <summary>
/// Turns the seed's snake_case fields into the rows of a stat block. Three tables and no
/// branching on category: an order, a hidden set, and a fallback that humanises whatever
/// Archives of Nethys prints next rather than dropping it.
/// </summary>
public static class MechanicsDisplay
{
    /// <summary>The order a player reads a stat block in, not the order the JSON happens to be in.</summary>
    static readonly (string Key, string Label)[] Known =
    [
        ("actions", "Actions"),
        ("component", "Components"),
        ("cost", "Cost"),
        ("range_raw", "Range"),
        ("range", "Range"),
        ("area_raw", "Area"),
        ("area", "Area"),
        ("area_type", "Area Type"),
        ("target", "Targets"),
        ("duration_raw", "Duration"),
        ("duration", "Duration"),
        ("onset_raw", "Onset"),
        ("onset", "Onset"),
        ("saving_throw", "Saving Throw"),
        ("trigger", "Trigger"),
        ("requirement", "Requirements"),
        ("prerequisite", "Prerequisites"),
        ("frequency", "Frequency"),
        ("usage", "Usage"),
        ("price_raw", "Price"),
        ("price", "Price"),
        ("bulk_raw", "Bulk"),
        ("bulk", "Bulk"),
        ("hands", "Hands"),
        ("damage", "Damage"),
        ("damage_die", "Damage Die"),
        ("damage_type", "Damage Type"),
        ("ac", "AC Bonus"),
        ("dex_cap", "Dex Cap"),
        ("check_penalty", "Check Penalty"),
        ("speed_penalty", "Speed Penalty"),
        ("strength", "Strength"),
        ("armor_category", "Armor Category"),
        ("armor_group", "Armor Group"),
        ("weapon_category", "Weapon Category"),
        ("weapon_group", "Weapon Group"),
        ("weapon_type", "Weapon Type"),
        ("ammunition", "Ammunition"),
        ("reload_raw", "Reload"),
        ("reload", "Reload"),
        ("hardness_raw", "Hardness"),
        ("hardness", "Hardness"),
        ("hp_raw", "Hit Points"),
        ("hp", "Hit Points"),
        ("speed_raw", "Speed"),
        ("speed", "Speed"),
        ("size", "Size"),
        ("base_item", "Base Item"),
        ("item_subcategory", "Item Subcategory"),
        ("item_bonus_value", "Item Bonus"),
        ("item_bonus_note", "Item Bonus Note"),
        ("item_bonus_consumable", "Consumable"),
        ("heighten", "Heightened"),
        ("heighten_level", "Heightened Levels"),
        ("tradition", "Traditions"),
        ("school", "School"),
        ("spell_type", "Spell Type"),
        ("sanctification", "Sanctification"),
        ("primary_check", "Primary Check"),
        ("secondary_check", "Secondary Checks"),
        ("secondary_casters_raw", "Secondary Casters"),
        ("secondary_casters", "Secondary Casters"),
        ("skill", "Skills"),
        ("attribute", "Attributes"),
        ("attribute_flaw", "Attribute Flaw"),
        ("class", "Classes"),
        ("archetype", "Archetypes"),
        ("feat", "Feats"),
        ("spell", "Spells"),
        ("bloodline", "Bloodlines"),
        ("domain", "Domains"),
        ("domain_primary", "Primary Domains"),
        ("domain_alternate", "Alternate Domains"),
        ("deity", "Deities"),
        ("deity_category", "Deity Category"),
        ("divine_font", "Divine Font"),
        ("cleric_spell", "Cleric Spell"),
        ("favored_weapon", "Favored Weapons"),
        ("patron_theme", "Patron Theme"),
        ("element", "Elements"),
        ("language", "Languages"),
        ("vision", "Vision"),
        ("is_general_background", "General Background"),
        ("attack_proficiency", "Attack Proficiency"),
        ("defense_proficiency", "Defense Proficiency"),
        ("fortitude_proficiency", "Fortitude Proficiency"),
        ("reflex_proficiency", "Reflex Proficiency"),
        ("will_proficiency", "Will Proficiency"),
        ("perception_proficiency", "Perception Proficiency"),
        ("skill_proficiency", "Skill Proficiency"),
        ("pfs", "PFS Legality"),
        ("primary_source_raw", "Source"),
        ("source_raw", "Also Printed In"),
        ("source", "Also Printed In"),
    ];

    /// <summary>Identifiers and bookkeeping that mean nothing at a table.</summary>
    static readonly HashSet<string> Hidden = new(StringComparer.Ordinal)
    {
        "trait_group",
        "remaster_id",
        "legacy_id",
        "item_parent_id",
        "item_child_id",
        "release_date",
        "source_category",
        "primary_source_category",
        "actions_number",
    };

    static readonly Dictionary<string, int> Order =
        Known.Select((entry, index) => (entry.Key, index))
             .ToDictionary(entry => entry.Key, entry => entry.index, StringComparer.Ordinal);

    static readonly Dictionary<string, string> Labels =
        Known.GroupBy(entry => entry.Key, StringComparer.Ordinal)
             .ToDictionary(group => group.Key, group => group.First().Label, StringComparer.Ordinal);

    public static IReadOnlyList<MechanicRow> Rows(IReadOnlyList<MechanicField> fields)
    {
        var populated = fields
            .Select(field => new MechanicField(field.Key, Trimmed(field.Values)))
            .Where(field => field.Values.Count > 0)
            .ToList();

        var keys = populated.Select(field => field.Key).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. populated
                .Where(field => !Hidden.Contains(field.Key))
                // The seed carries a numeric field beside its printed form, such as 14000 beside
                // "140 gp". Only the printed form means anything to a player.
                .Where(field => !keys.Contains(field.Key + "_raw"))
                .OrderBy(field => Order.GetValueOrDefault(field.Key, int.MaxValue))
                .Select(field => new MechanicRow(LabelOf(field.Key), field.Values)),
        ];
    }

    static string LabelOf(string key) =>
        Labels.TryGetValue(key, out var label) ? label : Humanised(key);

    static string Humanised(string key) =>
        string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    static IReadOnlyList<string> Trimmed(IReadOnlyList<string> values) =>
        [.. values.Select(value => value.Trim()).Where(value => value.Length > 0)];
}
