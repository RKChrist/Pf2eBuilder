using Microsoft.EntityFrameworkCore;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>The filters a search and a count share, so a count never disagrees with the list it
/// stands for.</summary>
internal static class RuleFilters
{
    const string Escape = "\\";

    public static IQueryable<RuleRecord> NameContains(this IQueryable<RuleRecord> records, string? name) =>
        name is { Length: > 0 }
            ? records.Where(r => EF.Functions.Like(r.Name, $"%{Escaped(name)}%", Escape))
            : records;

    public static bool HasTrait(IEnumerable<string> traits, string trait) =>
        traits.Contains(trait, StringComparer.OrdinalIgnoreCase);

    /// <summary>A name typed as "10%" means the characters, not a wildcard.</summary>
    static string Escaped(string text) =>
        text.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_");
}
