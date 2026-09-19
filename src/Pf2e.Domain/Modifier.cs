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
/// A named adjustment to whatever its selectors match. A positive <see cref="Value"/> is a
/// bonus, a negative one is a penalty.
/// </summary>
public sealed record Modifier(string Source, ModifierType Type, int Value, ImmutableArray<Selector> Applies)
{
    public bool IsBonus => Value > 0;

    public bool IsPenalty => Value < 0;

    public bool AppliesTo(StatTarget target) => Applies.Any(selector => selector.Matches(target));
}
