using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// The layer that changes every few minutes at the table. A re-import replaces the build and
/// leaves this alone, so a player who levels up mid-session keeps their damage.
/// </summary>
public sealed record SessionState(
    int CurrentHitPoints,
    int TemporaryHitPoints,
    int HeroPoints,
    ImmutableArray<ActiveEffect> Effects)
{
    public static SessionState Fresh(int maxHitPoints) => new(maxHitPoints, 0, 0, []);
}
