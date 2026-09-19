using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>One modifier a condition imposes. Conditions can impose more than one.</summary>
public abstract record ModifierTemplate(ModifierType Type, ImmutableArray<Selector> Applies)
{
    public abstract Modifier For(string conditionName, int conditionValue);

    /// <summary>A penalty equal to the condition's value, such as clumsy 2 giving -2.</summary>
    public sealed record Scaling(ModifierType Type, ImmutableArray<Selector> Applies)
        : ModifierTemplate(Type, Applies)
    {
        public override Modifier For(string name, int value) =>
            new($"{name} {value}", Type, -value, Applies);
    }

    /// <summary>A penalty that does not vary, such as prone's -2 to attack rolls.</summary>
    public sealed record Flat(ModifierType Type, int Penalty, ImmutableArray<Selector> Applies)
        : ModifierTemplate(Type, Applies)
    {
        public override Modifier For(string name, int value) => new(name, Type, -Penalty, Applies);
    }
}

/// <summary>
/// <see cref="Verified"/> defaults to false because the first draft of this table was
/// transcribed from a brief its own author called a draft written from memory.
/// </summary>
public sealed record ConditionDefinition(
    string Key,
    string Name,
    bool HasValue,
    ImmutableArray<ModifierTemplate> Templates,
    string SourceRef,
    bool Verified = false)
{
    public ImmutableArray<Modifier> ModifiersAt(int value = 0)
    {
        if (HasValue && value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{Name} always carries a value of 1 or more.");
        }

        if (!HasValue && value != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{Name} does not carry a value.");
        }

        return [.. Templates.Select(t => t.For(Name, value))];
    }
}

public static class Conditions
{
    const string Ref = "Player Core, Conditions Appendix";

    static ConditionDefinition Scaling(string key, string name, params Selector[] applies) =>
        new(key, name, HasValue: true,
            [new ModifierTemplate.Scaling(ModifierType.Status, [.. applies])], Ref);

    static ConditionDefinition Flat(string key, string name, ModifierType type, int penalty, params Selector[] applies) =>
        new(key, name, HasValue: false,
            [new ModifierTemplate.Flat(type, penalty, [.. applies])], Ref);

    // Every scaling condition below names a derived category rather than a list of statistics,
    // which is how the printed rules are written. Clumsy is "Dex-based", and that phrase covers
    // Dex-based attack rolls, which an enumeration of AC, Reflex and three skills silently drops.
    public static ConditionDefinition Clumsy { get; } =
        Scaling("clumsy", "Clumsy", Selector.Governed(AttributeKind.Dexterity));

    public static ConditionDefinition Drained { get; } =
        Scaling("drained", "Drained", Selector.Governed(AttributeKind.Constitution));

    public static ConditionDefinition Enfeebled { get; } =
        Scaling("enfeebled", "Enfeebled", Selector.Governed(AttributeKind.Strength));

    public static ConditionDefinition Stupefied { get; } =
        Scaling("stupefied", "Stupefied",
            Selector.Governed(AttributeKind.Intelligence),
            Selector.Governed(AttributeKind.Wisdom),
            Selector.Governed(AttributeKind.Charisma));

    public static ConditionDefinition Frightened { get; } =
        Scaling("frightened", "Frightened", Selector.AllChecksAndDcs);

    public static ConditionDefinition Sickened { get; } =
        Scaling("sickened", "Sickened", Selector.AllChecksAndDcs);

    public static ConditionDefinition Fascinated { get; } =
        Flat("fascinated", "Fascinated", ModifierType.Status, 2,
            Selector.Exactly(StatKind.Perception), Selector.Exactly(StatKind.Skill));

    public static ConditionDefinition Fatigued { get; } =
        Flat("fatigued", "Fatigued", ModifierType.Status, 1,
            Selector.Exactly(StatKind.ArmorClass), Selector.SavingThrows);

    // UNVERIFIED against the printed rule: deafened's penalty should be predicated on the
    // auditory trait, which this engine cannot express yet, so it applies to all Perception.
    public static ConditionDefinition Deafened { get; } =
        Flat("deafened", "Deafened", ModifierType.Status, 2, Selector.Exactly(StatKind.Perception));

    public static ConditionDefinition Blinded { get; } =
        Flat("blinded", "Blinded", ModifierType.Status, 4, Selector.Exactly(StatKind.Perception));

    public static ConditionDefinition Unconscious { get; } =
        Flat("unconscious", "Unconscious", ModifierType.Status, 4,
            Selector.Exactly(StatKind.ArmorClass),
            Selector.Exactly(StatKind.Perception),
            Selector.Exactly(StatKind.Reflex));

    public static ConditionDefinition OffGuard { get; } =
        Flat("off-guard", "Off-Guard", ModifierType.Circumstance, 2, Selector.Exactly(StatKind.ArmorClass));

    public static ConditionDefinition Prone { get; } =
        Flat("prone", "Prone", ModifierType.Circumstance, 2, Selector.Exactly(StatKind.Attack));

    /// <summary>Clumsy 1 and a speed penalty, which is two modifiers of different types.</summary>
    public static ConditionDefinition Encumbered { get; } =
        new("encumbered", "Encumbered", HasValue: false,
        [
            new ModifierTemplate.Flat(ModifierType.Status, 1, [Selector.Governed(AttributeKind.Dexterity)]),
            new ModifierTemplate.Flat(ModifierType.Untyped, 10, [Selector.Speeds]),
        ], Ref);

    public static ImmutableArray<ConditionDefinition> All { get; } =
    [
        Clumsy, Drained, Enfeebled, Stupefied, Frightened, Sickened,
        Fascinated, Fatigued, Deafened, Blinded, Unconscious, OffGuard, Prone, Encumbered,
    ];
}
