using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// Fields whose values may name another record, and the category that record would live in. A
/// value links only when a record of exactly that name exists there, which the server settles
/// in one query per sheet so a client never renders a link that lands on nothing. A table, not a
/// branch: a new reference field is a row.
/// </summary>
internal static class RuleLinks
{
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
    };

    public static async Task<IReadOnlyList<RuleLink>> Resolve(
        IRulesDbContext db, IReadOnlyList<MechanicField> mechanics, CancellationToken ct)
    {
        var wanted = mechanics
            .Where(field => CategoryOf.ContainsKey(field.Key))
            .SelectMany(field => field.Values.Select(value => (field.Key, Value: value, Category: CategoryOf[field.Key])))
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var categories = wanted.Select(w => w.Category).Distinct().ToList();
        var names = wanted.Select(w => w.Value.ToLower()).Distinct().ToList();

        var found = await db.RuleRecords.AsNoTracking()
            .Where(r => categories.Contains(r.Category) && names.Contains(r.Name.ToLower()))
            .Select(r => new { r.Id, r.Category, r.Name })
            .ToListAsync(ct);

        // Two records of one name in one category go to the one a name search ranks first.
        var byName = found
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .GroupBy(r => (r.Category, Name: r.Name.ToLowerInvariant()))
            .ToDictionary(group => group.Key, group => group.First().Id);

        return
        [
            .. wanted
                .Where(w => byName.ContainsKey((w.Category, w.Value.ToLowerInvariant())))
                .Select(w => new RuleLink(w.Key, w.Value, byName[(w.Category, w.Value.ToLowerInvariant())])),
        ];
    }
}
