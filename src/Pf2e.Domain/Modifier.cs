using System.Collections.Immutable;

namespace Pf2e.Domain;

public enum ModifierType
{
    Circumstance,
    Item,
    Status,
    Untyped,
}

/// <summary>
/// Where a modifier came from. Gear is what the character is carrying and is as permanent as the
/// build is; Effect is what somebody did to them, and is what a screen means when it marks a
/// number as changed. Both stack by the same rule, so this decides nothing about arithmetic.
/// </summary>
public enum ModifierOrigin
{
    Effect,
    Gear,
}

/// <summary>
/// A named adjustment to whatever its selectors match. A positive <see cref="Value"/> is a
/// bonus, a negative one is a penalty.
/// </summary>
public sealed record Modifier(
    string Source,
    ModifierType Type,
    int Value,
    ImmutableArray<Selector> Applies,
    ModifierOrigin Origin = ModifierOrigin.Effect)
{
    public bool IsBonus => Value > 0;

    public bool IsPenalty => Value < 0;

    public bool AppliesTo(StatTarget target) => Applies.Any(selector => selector.Matches(target));
}
