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
    int Total,
    int Bare)
{
    public IEnumerable<string> ContributingSources => Applied.Select(m => m.Source);

    /// <summary>How far the effects on this creature have moved the number, which is not the same
    /// as the sum of the applied effect modifiers: an effect can suppress a worn item bonus, and
    /// then taking the effect off gives the worn one back rather than losing both.</summary>
    public int Swing => Total - Bare;
}
