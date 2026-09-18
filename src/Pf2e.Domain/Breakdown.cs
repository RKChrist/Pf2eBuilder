using System.Collections.Immutable;

namespace Pf2e.Domain;

public sealed record SuppressedModifier(Modifier Modifier, string Reason);

/// <summary>
/// A computed statistic together with everything that went into it, so the number can
/// explain itself to the player.
/// </summary>
public sealed record Breakdown(
    int Base,
    ImmutableArray<Modifier> Applied,
    ImmutableArray<SuppressedModifier> Suppressed,
    int Total)
{
    public IEnumerable<string> ContributingSources => Applied.Select(m => m.Source);
}
