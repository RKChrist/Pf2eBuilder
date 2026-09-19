using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// A modifier in the form a player types, a seed file carries and a database column holds.
/// <see cref="ToModifier"/> turns it into the thing the stacking rule consumes. As with
/// <see cref="Modifier"/>, a positive <see cref="Value"/> is a bonus and a negative one a penalty.
/// </summary>
public sealed record EffectModifier(
    ModifierType Type,
    int Value,
    ImmutableArray<SelectorSpec> Applies)
{
    // A default ImmutableArray is not an empty one and throws on enumeration, and this type is
    // reached by deserialization, where an absent applies list arrives as exactly that.
    public ImmutableArray<SelectorSpec> Applies { get; init; } = Applies.IsDefault ? [] : Applies;

    public Modifier ToModifier(string source) =>
        new(source, Type, Value, [.. Applies.Select(spec => spec.ToSelector())]);
}
