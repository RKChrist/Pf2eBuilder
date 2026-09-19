using System.Globalization;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Catalog;

/// <summary>
/// A spell's heightening in words. The seed prints a step, such as "+2", or the ranks themselves,
/// such as "5th" and "7th". A step counts from the spell's own rank, so a 3rd-rank spell with
/// "+1" first changes at 4th.
/// </summary>
public static class Heightening
{
    public static IReadOnlyList<string> Read(IReadOnlyList<string> values, RuleSummary spell)
    {
        var steps = values
            .Select(value => value.StartsWith('+') && int.TryParse(value[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var step) ? step : (int?)null)
            .ToList();

        var ranks = values.Where((_, index) => steps[index] is null).ToList();

        return
        [
            .. steps.OfType<int>().Select(step => Every(step, spell.Level)),
            .. ranks.Count > 0 ? [$"At {string.Join(", ", ranks)}"] : Array.Empty<string>(),
        ];
    }

    static string Every(int step, int? rank)
    {
        var every = step == 1 ? "Every rank" : $"Every {step} ranks";
        return rank is int from ? $"{every} from {Ordinal(from + step)}" : every;
    }

    static string Ordinal(int number) => (number % 100, number % 10) switch
    {
        (11 or 12 or 13, _) => $"{number}th",
        (_, 1) => $"{number}st",
        (_, 2) => $"{number}nd",
        (_, 3) => $"{number}rd",
        _ => $"{number}th",
    };
}
