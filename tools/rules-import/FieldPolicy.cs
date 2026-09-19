using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

static class FieldPolicy
{
    public static readonly IReadOnlyList<string> Categories =
    [
        "equipment", "feat", "action", "spell", "class-feature", "trait", "deity", "background",
        "weapon", "heritage", "archetype", "ritual", "language", "condition", "ancestry",
        "armor", "class", "skill",

        // Every first-level subclass choice lives in its own category. A class-feature record
        // holds the empty slot; these hold the options. Without them a sorcerer has no
        // spellcasting tradition and a barbarian has no instinct.
        "lesson", "bloodline", "arcane-school", "patron", "eidolon", "mystery", "ikon",
        "implement", "epithet", "instinct", "hybrid-study", "apparition", "cause",
        "druidic-order", "conscious-mind", "style", "way", "arcane-thesis", "racket",
        "methodology", "muse", "research-field", "subconscious-mind", "hunters-edge",
        "innovation", "element", "doctrine", "tradition", "grim-fascination", "practice",
        "fatal-method",

        // shield is the only place ac, hardness and hp_raw exist anywhere in the data.
        // item-bonus carries values that are empty on the parent equipment record.
        // source is the rulebook list the Manage Available Rulebooks screen needs.
        "shield", "item-bonus", "source", "domain", "familiar-ability", "familiar-specific",
        "animal-companion", "animal-companion-specialization", "animal-companion-advanced",
        "animal-companion-unique", "follower", "weapon-group", "armor-group", "runesmith-rune",
        "draconic-exemplar", "tactic", "class-kit", "relic", "set-relic", "curse",
        "deity-category", "mythic-calling", "skill-general-action", "hellknight-order",
        "deviant-ability-classification",

        // The encounter side. Creature records carry hp, ac, the three saves, perception,
        // speed, traits and attribute modifiers as structured fields, so a DM searches for an
        // Ogre Warrior instead of typing one in mid-fight.
        "creature", "creature-family", "creature-ability",
    ];

    // What we withhold is Paizo's expression, meaning flavour text and rule descriptions. Short
    // mechanical labels such as a trigger or a prerequisite are stat-block headers that a character
    // builder cannot work without, so they stay. Withheld prose is never downloaded at all; these
    // patterns go into _source.excludes on every request, and AoN honours wildcards there.
    public static readonly IReadOnlyList<string> WireExcludes =
    [
        "markdown", "*_markdown", "access", "anathema", "area_of_concern", "area_of_concern_raw",
        "edict", "religious_symbol", "sanctification_raw", "stage", "summary", "text",
    ];

    public static readonly IReadOnlyList<string> ProseFieldOrder =
    [
        "access", "ammunition_markdown", "anathema", "area_markdown", "area_of_concern",
        "area_of_concern_raw", "armor_group_markdown", "base_item_markdown", "bloodline_markdown",
        "cost_markdown", "deity_category_markdown", "deity_markdown", "divine_font_markdown",
        "domain_alternate_markdown", "domain_markdown", "domain_primary_markdown", "edict",
        "favored_weapon_markdown", "feat_markdown", "language_markdown", "markdown",
        "pantheon_markdown", "pantheon_member_markdown", "patron_theme_markdown",
        "prerequisite_markdown", "primary_check_markdown", "religious_symbol", "requirement_markdown",
        "sanctification_raw", "saving_throw_markdown", "search_markdown",
        "secondary_check_markdown", "skill_markdown", "source_markdown", "speed_markdown",
        "spell_markdown", "stage", "stage_markdown", "summary", "summary_markdown", "target_markdown",
        "text",
        "tradition_markdown", "trait_markdown", "trigger_markdown", "usage_markdown",
        "weapon_group_markdown",
    ];

    static readonly FrozenSet<string> ProseFields = ProseFieldOrder.ToFrozenSet(StringComparer.Ordinal);

    // Deny-by-default, and load-bearing for the licensing guarantee: a field absent from this set
    // never reaches the seed, so a new AoN field is withheld until someone reviews and lists it.
    public static readonly FrozenSet<string> SeedAllowList = new[]
    {
        "hardness", "hardness_raw", "item_bonus_value", "item_bonus_note", "item_bonus_consumable", "item_subcategory", "trait_group", "source_raw", "fortitude_save", "reflex_save", "will_save", "perception", "dexterity", "constitution", "intelligence", "wisdom", "charisma", "sense", "creature_family", "npc", "strike_damage_average", "attack_bonus", "strongest_save", "weakest_save", "size_id", "alignment", "trait_raw", "ac", "actions", "actions_number", "ammunition", "archetype", "area", "area_raw", "area_type",
        "armor_category", "armor_group", "attack_proficiency", "attribute", "attribute_flaw",
        "base_item", "bloodline", "bulk", "bulk_raw", "check_penalty", "class", "cleric_spell",
        "component", "cost", "damage", "damage_die", "damage_type", "defense_proficiency", "deity",
        "deity_category", "dex_cap", "divine_font", "domain", "domain_alternate", "domain_primary",
        "duration", "duration_raw", "element", "favored_weapon", "feat", "fortitude_proficiency",
        "frequency", "hands", "heighten", "heighten_level", "hp", "hp_raw", "is_general_background",
        "item_child_id", "item_parent_id", "language", "legacy_id", "level", "onset", "onset_raw",
        "patron_theme", "perception_proficiency", "pfs", "prerequisite", "price", "price_raw",
        "primary_check", "primary_source", "primary_source_category", "primary_source_raw", "range",
        "range_raw", "rarity", "reflex_proficiency", "release_date", "reload", "reload_raw",
        "remaster_id", "requirement", "resistance", "sanctification", "saving_throw", "school",
        "secondary_casters", "secondary_casters_raw", "secondary_check", "size", "skill", "skill_mod",
        "skill_proficiency", "source", "source_category", "speed", "speed_penalty", "speed_raw",
        "spell", "spell_type", "strength", "target", "tradition", "trait", "trigger", "type", "usage",
        "vision", "weakness", "weapon_category", "weapon_group", "weapon_type", "will_proficiency",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The fields listed under this ceiling are on the allow-list as stat-block labels. A value that
    // runs longer has stopped being a label and is rule text AoN filed under a header field, which is
    // Paizo's expression and not ours to redistribute. Such a value is dropped rather than truncated,
    // because an absent field is obviously absent while a truncated one looks like data. The ceiling
    // covers the whole group and not just requirement and frequency, the two with a measured prose
    // tail, so a field AoN starts overloading next year is covered without a code change.
    public const int LabelCeiling = 300;

    public static readonly FrozenSet<string> CeilingFields = new[]
    {
        "area_raw", "cost", "duration_raw", "frequency", "pfs", "prerequisite", "primary_check",
        "requirement", "secondary_casters_raw", "secondary_check", "target", "trigger", "usage",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static int RenderedLength(JsonNode value) =>
        value is JsonValue v && v.TryGetValue<string>(out var text)
            ? text.Length
            : value.ToJsonString().Length;

    // A non-empty remaster_id marks the record as superseded by the ids it names. One definition
    // serves both the pull that drops those records and the transform that reports the survivors.
    // AoN writes remaster_id ["0"] on records that have no successor, so "0" is a marker and not an
    // id. Verified on 18 Sep 2026: exactly 72 documents across the eighteen categories carry it, and
    // they are the only ones whose remaster_id resolves to nothing.
    const string NoSuccessor = "0";

    public static List<string> LegacyTargets(JsonObject record)
    {
        if (record["remaster_id"] is not JsonArray remaster)
        {
            return [];
        }

        var targets = new List<string>(remaster.Count);
        foreach (var node in remaster)
        {
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 &&
                text != NoSuccessor)
            {
                targets.Add(text);
            }
        }

        return targets;
    }

    // AoN marks its own non-entries this way: item-activation fragments filed as actions, and
    // superseded duplicates it keeps so old links still resolve. Neither is a rule to show anyone.
    public static bool IsSiteExcluded(JsonObject record) =>
        record["exclude_from_search"] is JsonValue flag && flag.TryGetValue<bool>(out var excluded) && excluded;

    public static bool IsProseField(string name) =>
        name.EndsWith("_markdown", StringComparison.Ordinal) || ProseFields.Contains(name);

    public static bool IsEmptyValue(JsonNode? value) => value switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        JsonValue v => v.TryGetValue<string>(out var s) && s.Length == 0,
        _ => false,
    };
}
