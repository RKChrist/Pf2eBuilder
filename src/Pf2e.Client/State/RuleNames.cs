using Pf2e.Client.Api;

namespace Pf2e.Client.State;

/// <summary>
/// Which record a rule name belongs to, kept for as long as the tab is open.
/// <para>A table taps the same handful of names all evening, and the ruleset does not change
/// under them, so the second tap on Frightened has no business waiting on the network again.</para>
/// </summary>
public sealed class RuleNames(RulesApi api)
{
    readonly Dictionary<(string? Category, string Name), string?> _known = new();

    public async Task<string?> IdOfAsync(string? category, string name, CancellationToken ct)
    {
        if (_known.TryGetValue((category, name), out var remembered))
        {
            return remembered;
        }

        try
        {
            var found = await api.FindAsync(category, name, ct);

            // A null is worth remembering: the ruleset holding no record under this name is an
            // answer, and asking again cannot change it. A failure is not, because the service
            // being unreachable is temporary and the next tap deserves a fresh try.
            _known[(category, name)] = found?.Id;
            return found?.Id;
        }
        catch (RulesApiException)
        {
            return null;
        }
    }
}
