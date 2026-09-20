using Microsoft.EntityFrameworkCore;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>The filters a search and a count share, so a count never disagrees with the list it
/// stands for.</summary>
internal static class RuleFilters
{
    const string Escape = "\\";

    public static IQueryable<RuleRecord> InCategory(this IQueryable<RuleRecord> records, string? category) =>
        category is { Length: > 0 } ? records.Where(r => r.Category == category) : records;

    /// <summary>
    /// Every word typed, anywhere in the name, in any order.
    /// <para>It was the whole phrase as one run of characters, so "raise shield" found nothing:
    /// the action is Raise a Shield, and nobody at a table types the article. One clause per word
    /// also finds "shield raise", which costs nothing and is what somebody half-remembering a
    /// name types.</para>
    /// </summary>
    public static IQueryable<RuleRecord> NameContains(this IQueryable<RuleRecord> records, string? name)
    {
        foreach (var word in Words(name))
        {
            var pattern = $"%{Escaped(word)}%";
            records = records.Where(r => EF.Functions.Like(r.Name, pattern, Escape));
        }

        return records;
    }

    /// <summary>Bounded, because each word is a clause and a pasted paragraph is not a search.</summary>
    public static IReadOnlyList<string> Words(string? name) =>
        [.. (name ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(8)];

    public static IQueryable<RuleRecord> LevelBetween(this IQueryable<RuleRecord> records, int? min, int? max)
    {
        if (min is int low)
        {
            records = records.Where(r => r.Level >= low);
        }

        return max is int high ? records.Where(r => r.Level <= high) : records;
    }

    public static bool HasTrait(IEnumerable<string> traits, string trait) =>
        traits.Contains(trait, StringComparer.OrdinalIgnoreCase);

    /// <summary>A name typed as "10%" means the characters, not a wildcard.</summary>
    static string Escaped(string text) =>
        text.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_");
}
