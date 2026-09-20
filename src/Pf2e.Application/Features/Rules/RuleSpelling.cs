using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// What somebody meant to type.
/// <para>A search box at a table is typed fast and with a thumb, and "shiedl" finding nothing reads
/// as the app not knowing what a shield is. A word that appears in no name at all is swapped for
/// the closest word that does, and only then: a word that matches something is left exactly as
/// typed, so a search that works never changes under the reader.</para>
/// <para>The vocabulary is every word in every record's name, which is a few thousand short
/// strings. It is read once and kept, because the ruleset only changes when the server restarts
/// and reseeds.</para>
/// </summary>
internal static class RuleSpelling
{
    /// <summary>Short words are left alone. "of" one letter out is "on", "or" and "if", and
    /// guessing between them is worse than finding nothing.</summary>
    const int ShortestWordCorrected = 4;

    static IReadOnlyList<(string Word, int Names)>? _vocabulary;

    /// <summary>The query with each unknown word replaced, or the query itself when every word
    /// is already in some name or nothing close enough exists.</summary>
    public static async Task<string?> CorrectAsync(IRulesDbContext db, string? name, CancellationToken ct)
    {
        var words = RuleFilters.Words(name);
        if (words.Count == 0)
        {
            return name;
        }

        var vocabulary = _vocabulary ??= await ReadAsync(db, ct);
        var corrected = words.Select(word => Closest(word, vocabulary) ?? word).ToList();
        return corrected.SequenceEqual(words, StringComparer.OrdinalIgnoreCase) ? name : string.Join(' ', corrected);
    }

    static async Task<IReadOnlyList<(string Word, int Names)>> ReadAsync(IRulesDbContext db, CancellationToken ct) =>
        [.. (await db.RuleRecords.AsNoTracking().Select(r => r.Name).ToListAsync(ct))
            .SelectMany(name => name.ToLowerInvariant()
                .Split([' ', '-', '\'', '(', ')', ',', '/', ':'], StringSplitOptions.RemoveEmptyEntries)
                .Distinct())
            .GroupBy(word => word)
            .Select(group => (group.Key, group.Count()))];

    static string? Closest(string typed, IReadOnlyList<(string Word, int Names)> vocabulary)
    {
        var word = typed.ToLowerInvariant();
        if (word.Length < ShortestWordCorrected || vocabulary.Any(known => known.Word.Contains(word, StringComparison.Ordinal)))
        {
            return null;
        }

        // One slip in a short word, two in a long one. More than that is a different word.
        var allowed = word.Length >= 7 ? 2 : 1;

        return vocabulary
            .Where(known => Math.Abs(known.Word.Length - word.Length) <= allowed)
            .Select(known => (known.Word, known.Names, Distance: Slips(word, known.Word, allowed)))
            .Where(candidate => candidate.Distance <= allowed)
            .OrderBy(candidate => candidate.Distance)
            // People get the first letter right far more often than any other.
            .ThenBy(candidate => candidate.Word[0] == word[0] ? 0 : 1)
            .ThenByDescending(candidate => candidate.Names)
            .ThenBy(candidate => candidate.Word, StringComparer.Ordinal)
            .Select(candidate => candidate.Word)
            .FirstOrDefault();
    }

    /// <summary>Insertions, deletions, substitutions and two neighbouring letters swapped, which
    /// is the slip "shiedl" is. Gives up past <paramref name="limit"/>, because the caller only
    /// wants to know whether the answer is small.</summary>
    static int Slips(string from, string to, int limit)
    {
        var previous2 = new int[to.Length + 1];
        var previous = Enumerable.Range(0, to.Length + 1).ToArray();
        var current = new int[to.Length + 1];

        for (var i = 1; i <= from.Length; i++)
        {
            current[0] = i;
            var best = current[0];
            for (var j = 1; j <= to.Length; j++)
            {
                var cost = from[i - 1] == to[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && from[i - 1] == to[j - 2] && from[i - 2] == to[j - 1])
                {
                    current[j] = Math.Min(current[j], previous2[j - 2] + 1);
                }

                best = Math.Min(best, current[j]);
            }

            if (best > limit)
            {
                return limit + 1;
            }

            (previous2, previous, current) = (previous, current, previous2);
        }

        return previous[to.Length];
    }
}
