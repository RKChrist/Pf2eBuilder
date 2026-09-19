using Microsoft.EntityFrameworkCore;
using Pf2e.Domain;

namespace Pf2e.Api.Features.Rules;

public sealed record ConditionLink(ConditionDefinition Definition, RuleRecord? Record);

/// <summary>
/// Joins the engine's condition registry to the seeded Archives of Nethys records. The registry
/// holds the modifiers, because AoN publishes a condition's text but not its structured effect.
/// The seed holds the identity and the deep link. The sheet needs both to show a penalty and say
/// where the rule lives.
/// </summary>
public static class ConditionLinks
{
    /// <summary>Registry keys are already the seeded name lowercased with spaces as hyphens.</summary>
    public static string SlugOf(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');

    public static async Task<IReadOnlyList<ConditionLink>> ResolveAsync(
        this Pf2e.Api.Infrastructure.Persistence.RulesDbContext db, CancellationToken ct = default)
    {
        var seeded = await db.RuleRecords
            .Where(r => r.Category == "condition")
            .ToListAsync(ct);

        var bySlug = seeded
            .GroupBy(r => SlugOf(r.Name))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return [.. Conditions.All.Select(c => new ConditionLink(c, bySlug.GetValueOrDefault(c.Key)))];
    }
}
