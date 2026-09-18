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
/// A named adjustment to one or more statistics. A positive <see cref="Value"/> is a bonus,
/// a negative one is a penalty.
/// </summary>
public sealed record Modifier(string Source, ModifierType Type, int Value, ImmutableArray<StatTarget> Targets)
{
    public bool IsBonus => Value > 0;

    public bool IsPenalty => Value < 0;

    public bool AppliesTo(StatTarget target) => Targets.Any(t => t.Matches(target));
}
