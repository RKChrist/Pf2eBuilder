using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Catalog;

public sealed record MechanicRow(string Key, string Label, IReadOnlyList<string> Values);

/// <summary>Tables, not a branch on category. Adding a per-category case here is the mistake.</summary>
public static class MechanicsDisplay
{
    static readonly (string Key, string Label)[] ReadingOrder =
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
        ("heighten", "Heightened"),
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
        // The traits again, as one string. The chips at the top of the panel are the same list and
        // each of those can be tapped.
        "trait_raw",
        // Every rank from the spell's own to 10th as a list of numbers; the heighten row already
        // says the same thing in words.
        "heighten_level",
    };

    /// <summary>A yes-or-no field is a badge when it is yes and nothing when it is no, because
    /// "Consumable: false" tells a reader nothing they would ask.</summary>
    static readonly (string Key, string Label)[] Flags =
    [
        ("is_general_background", "General background"),
        ("item_bonus_consumable", "Consumable"),
    ];

    public static IReadOnlyList<string> FlagsOf(RuleDetail rule) =>
        [.. Flags.Where(flag => rule.Mechanics.Any(field => field.Key == flag.Key && field.Values is ["true"]))
                 .Select(flag => flag.Label)];

    /// <summary>A field whose printed form needs the record around it to read well. Keyed by field,
    /// like everything else here.</summary>
    static readonly Dictionary<string, Func<IReadOnlyList<string>, RuleSummary, IReadOnlyList<string>>> Readings =
        new(StringComparer.Ordinal)
        {
            ["heighten"] = Heightening.Read,
        };

    /// <summary>What a player has to check before they may act at all, so it is never buried
    /// under the facts about the thing.</summary>
    static readonly HashSet<string> Gates = new(StringComparer.Ordinal)
    {
        "prerequisite",
        "trigger",
        "requirement",
    };

    public static bool IsGate(string key) => Gates.Contains(key);

    /// <summary>The record's highlights and its gates lead as a stat block; everything else
    /// follows. Both halves keep the reading order.</summary>
    public static (IReadOnlyList<MechanicRow> Lead, IReadOnlyList<MechanicRow> After) Split(RuleDetail rule)
    {
        var leading = rule.Summary.Highlights.Select(field => field.Key).Concat(Gates).ToHashSet(StringComparer.Ordinal);
        var rows = Rows(rule);

        return ([.. rows.Where(row => leading.Contains(row.Key))], [.. rows.Where(row => !leading.Contains(row.Key))]);
    }

    /// <summary>Keys under which the seed reprints a fact it has already stated elsewhere.</summary>
    static readonly (string Key, string Echoes)[] Repeats =
    [
        ("source_raw", "primary_source_raw"),
    ];

    static readonly Dictionary<string, int> Order =
        ReadingOrder.Select((entry, index) => (entry.Key, index))
             .ToDictionary(entry => entry.Key, entry => entry.index, StringComparer.Ordinal);

    static readonly Dictionary<string, string> Labels =
        ReadingOrder.GroupBy(entry => entry.Key, StringComparer.Ordinal)
             .ToDictionary(group => group.Key, group => group.First().Label, StringComparer.Ordinal);

    /// <summary>
    /// A record restates its own name under a field named after its own category: a feat carries
    /// <c>feat</c>, a domain carries <c>domain</c>, and "Feats: Shield Block" under the heading
    /// "Shield Block" reads as a defect. Only that field is dropped. Matching the name against
    /// every field instead would cost the Club its weapon group and Unarmored its armour
    /// category, which are facts about the record rather than echoes of it.
    /// </summary>
    public static IReadOnlyList<MechanicRow> Rows(RuleDetail rule)
    {
        var selfReference = rule.Summary.Category.Replace('-', '_');

        var populated = rule.Mechanics
            .Select(field => new MechanicField(
                field.Key,
                Trimmed(field.Values, field.Key == selfReference ? rule.Summary.Name : null)))
            .Where(field => field.Values.Count > 0)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);

        return
        [
            .. populated.Values
                .Where(field => !Hidden.Contains(field.Key) && !Flags.Any(flag => flag.Key == field.Key))
                // The seed carries a numeric field beside its printed form, such as 14000 beside
                // "140 gp". Only the printed form means anything to a player.
                .Where(field => !populated.ContainsKey(field.Key + "_raw"))
                .Where(field => !Echoes(field, populated))
                .OrderBy(field => Order.GetValueOrDefault(field.Key, int.MaxValue))
                .Select(field => new MechanicRow(field.Key, LabelOf(field.Key),
                    Readings.TryGetValue(field.Key, out var read) ? read(field.Values, rule.Summary) : field.Values)),
        ];
    }

    static bool Echoes(MechanicField field, IReadOnlyDictionary<string, MechanicField> present) =>
        Repeats.Any(repeat => repeat.Key == field.Key
                              && present.TryGetValue(repeat.Echoes, out var original)
                              && original.Values.SequenceEqual(field.Values, StringComparer.Ordinal));

    public static string LabelOf(string key) =>
        Labels.TryGetValue(key, out var label) ? label : Humanised(key);

    static string Humanised(string key) =>
        string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    static IReadOnlyList<string> Trimmed(IReadOnlyList<string> values, string? echoed) =>
        [.. values.Select(value => value.Trim())
                  .Where(value => value.Length > 0
                                  && !value.Equals(echoed, StringComparison.OrdinalIgnoreCase))];
}
