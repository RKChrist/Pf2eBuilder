using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// Where an effect's modifiers came from. The calculator does not care, because the stacking
/// rule is the same for a condition, a spell and a GM's ruling. Provenance exists so the sheet
/// can link back to a printed rule and so the screen can tell a player which of these they are
/// allowed to edit.
/// </summary>
public abstract record EffectSource
{
    private EffectSource() { }

    /// <summary>"Seeded", "Rule" or "Custom". One column of the three a row and a wire view
    /// both carry, and the only vocabulary either of them needs to know.</summary>
    public string Kind => this switch
    {
        Seeded => nameof(Seeded),
        Rule => nameof(Rule),
        Custom => nameof(Custom),
        _ => throw new InvalidOperationException($"{GetType().Name} is not an effect source."),
    };

    /// <summary>A registry key or a rule id, and null for a custom effect.</summary>
    public string? StoredKey => this switch
    {
        Seeded seeded => seeded.Key,
        Rule rule => rule.RuleId,
        Custom => null,
        _ => throw new InvalidOperationException($"{GetType().Name} is not an effect source."),
    };

    /// <summary>Empty for a seeded effect, whose modifiers are resolved through the registry
    /// rather than copied.</summary>
    public ImmutableArray<EffectModifier> StoredModifiers => this switch
    {
        Seeded => [],
        Rule rule => rule.Modifiers,
        Custom custom => custom.Modifiers,
        _ => throw new InvalidOperationException($"{GetType().Name} is not an effect source."),
    };

    /// <summary>The way back from the three columns. An unrecognised kind is a hand-edited row
    /// rather than an old one, because nothing but this type ever writes the column.</summary>
    public static EffectSource From(string kind, string? key, ImmutableArray<EffectModifier> modifiers) => kind switch
    {
        nameof(Seeded) => new Seeded(key ?? string.Empty),
        nameof(Rule) => new Rule(key ?? string.Empty, modifiers),
        nameof(Custom) => new Custom(modifiers),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an effect source kind."),
    };

    /// <summary>One of the effects in <see cref="Effects.All"/>, resolved by key so a
    /// scaling condition always reflects its current value.</summary>
    public sealed record Seeded(string Key) : EffectSource;

    /// <summary>A seeded rule record whose extracted modifiers were copied when it was applied.
    /// They are copied rather than re-read so that re-seeding cannot silently change a number
    /// mid-combat; re-applying the effect picks up the new data.</summary>
    public sealed record Rule(string RuleId, ImmutableArray<EffectModifier> Modifiers) : EffectSource;

    /// <summary>Typed at the table by a player.</summary>
    public sealed record Custom(ImmutableArray<EffectModifier> Modifiers) : EffectSource;
}

/// <summary>
/// One effect on one character.
/// <para><see cref="Id"/> is on the effect because the same effect can legitimately be applied
/// twice: two allies can each grant a +1 status bonus, and the stacking rule's job is to
/// suppress one of them visibly. A key-addressed list would collapse them before the rule got
/// to speak.</para>
/// <para><see cref="Duration"/> is a label the screen shows and a human clears. Nothing
/// decrements it. There is no clock in <see cref="CharacterSheet.Compute"/> and no initiative in
/// this product. It is stored because an effect has a duration and adding the column later is a
/// migration worth avoiding, so do not go looking for the timer.</para>
/// </summary>
public sealed record ActiveEffect(
    Guid Id,
    string Name,
    int Value,
    EffectSource Source,
    string? Duration = null)
{
    /// <summary>The stored value read as the registry will accept it. A valued effect is at
    /// least 1 and a flat one is 0, because <see cref="EffectDefinition.ModifiersAt"/> throws
    /// otherwise and this reads persisted state that a registry change can outdate.</summary>
    public int ScaledValue => Definition is { HasValue: true } ? Math.Max(1, Value) : 0;

    /// <summary>The one path from an effect to the stacking rule. Every source arrives here.</summary>
    public ImmutableArray<Modifier> Modifiers() =>
        Source is EffectSource.Seeded
            ? Definition?.ModifiersAt(ScaledValue) ?? []
            : [.. Source.StoredModifiers.Select(modifier => modifier.ToModifier(Name))];

    /// <summary>Null for a custom or rule effect, and also for a seeded key the registry has
    /// since dropped, which is skipped rather than thrown on so one stale row cannot break a
    /// whole table.</summary>
    public EffectDefinition? Definition =>
        Source is EffectSource.Seeded seeded ? Effects.Find(seeded.Key) : null;
}
