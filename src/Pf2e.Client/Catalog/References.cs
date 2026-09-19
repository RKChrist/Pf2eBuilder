namespace Pf2e.Client.Catalog;

/// <summary>
/// Fields whose values name another record, and the category that record lives in. A value in
/// one of these reads as a link to that record, resolved by its exact name. A table, like the
/// rest of the catalog: a new reference field is a row, not a branch in the sheet.
/// </summary>
public static class References
{
    public const string TraitCategory = "trait";

    static readonly Dictionary<string, string> CategoryOf = new(StringComparer.Ordinal)
    {
        ["archetype"] = "archetype",
        ["feat"] = "feat",
        ["spell"] = "spell",
        ["deity"] = "deity",
        ["deity_category"] = "deity-category",
        ["domain"] = "domain",
        ["domain_primary"] = "domain",
        ["domain_alternate"] = "domain",
        ["skill"] = "skill",
        ["class"] = "class",
        ["bloodline"] = "bloodline",
        ["tradition"] = "tradition",
        ["language"] = "language",
        ["element"] = "element",
        ["weapon_group"] = "weapon-group",
        ["armor_group"] = "armor-group",
        ["favored_weapon"] = "weapon",
        ["trait"] = TraitCategory,
    };

    public static string? CategoryFor(string field) => CategoryOf.GetValueOrDefault(field);
}
