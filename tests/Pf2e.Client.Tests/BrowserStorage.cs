using Microsoft.JSInterop;

namespace Pf2e.Client.Tests;

/// <summary>
/// The three calls <see cref="Pf2e.Client.State.CampaignMemory"/> makes into the page, answered
/// out of a dictionary. Storage is the whole subject here, so a fake that only records the calls
/// would prove nothing about what comes back on the next read.
/// </summary>
public sealed class BrowserStorage : IJSRuntime
{
    readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

    /// <summary>Set to make every call throw, which is a private window with storage switched
    /// off, and the reason every call in the store swallows its failure.</summary>
    public bool Broken { get; set; }

    public string? this[string key]
    {
        get => _items.TryGetValue(key, out var stored) ? stored : null;
        set
        {
            if (value is null)
            {
                _items.Remove(key);
            }
            else
            {
                _items[key] = value;
            }
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        ValueTask.FromResult((TValue)Call(identifier, args)!);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    object? Call(string identifier, object?[]? args)
    {
        if (Broken)
        {
            throw new JSException("localStorage is not available.");
        }

        var given = args ?? [];
        switch (identifier)
        {
            case "localStorage.getItem":
                return this[(string)given[0]!];
            case "localStorage.setItem":
                this[(string)given[0]!] = (string)given[1]!;
                return null;
            case "localStorage.removeItem":
                this[(string)given[0]!] = null;
                return null;
            default:
                throw new InvalidOperationException($"Nothing fakes {identifier}.");
        }
    }
}

/// <summary>A clock that does not move unless a test moves it, so "most recently opened first"
/// is asserted against stated times rather than against how fast the machine ran.</summary>
public sealed class StoppedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
