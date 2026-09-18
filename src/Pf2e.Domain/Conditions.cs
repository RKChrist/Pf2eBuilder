using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// A condition that imposes a modifier. <see cref="Verified"/> defaults to false because the
/// values were transcribed from a brief its own author called a draft written from memory.
/// </summary>
public abstract record ConditionDefinition(
    string Key,
    string Name,
    ModifierType Type,
    ImmutableArray<StatTarget> Targets,
    string SourceRef,
    bool Verified = false);

/// <summary>A condition with a value, such as clumsy 2, whose penalty equals that value.</summary>
public sealed record ValuedCondition(
    string Key,
    string Name,
    ModifierType Type,
    ImmutableArray<StatTarget> Targets,
    string SourceRef,
    bool Verified = false)
    : ConditionDefinition(Key, Name, Type, Targets, SourceRef, Verified)
{
    public Modifier AtValue(int value) => new($"{Name} {value}", Type, -value, Targets);
}

/// <summary>A condition without a value, such as prone, whose penalty is fixed.</summary>
public sealed record FixedCondition(
    string Key,
    string Name,
    ModifierType Type,
    int Penalty,
    ImmutableArray<StatTarget> Targets,
    string SourceRef,
    bool Verified = false)
    : ConditionDefinition(Key, Name, Type, Targets, SourceRef, Verified)
{
    public Modifier Modifier => new(Name, Type, -Penalty, Targets);
}

public static class Conditions
{
    private const string PlayerCoreConditions = "Player Core, Conditions Appendix";

    private static readonly ImmutableArray<StatTarget> ClumsyTargets =
    [
        new(StatKind.ArmorClass),
        new(StatKind.Reflex),
        StatTarget.Skill("Acrobatics"),
        StatTarget.Skill("Stealth"),
        StatTarget.Skill("Thievery"),
    ];

    public static ValuedCondition Clumsy { get; } =
        new("clumsy", "Clumsy", ModifierType.Status, ClumsyTargets, PlayerCoreConditions);

    public static ValuedCondition Drained { get; } =
        new("drained", "Drained", ModifierType.Status, [new(StatKind.Fortitude)], PlayerCoreConditions);

    public static ValuedCondition Enfeebled { get; } = new(
        "enfeebled",
        "Enfeebled",
        ModifierType.Status,
        [new(StatKind.Attack), new(StatKind.Damage), StatTarget.Skill("Athletics")],
        PlayerCoreConditions);

    public static ValuedCondition Frightened { get; } =
        new("frightened", "Frightened", ModifierType.Status, StatTarget.AllChecks, PlayerCoreConditions);

    public static ValuedCondition Sickened { get; } =
        new("sickened", "Sickened", ModifierType.Status, StatTarget.AllChecks, PlayerCoreConditions);

    public static ValuedCondition Stupefied { get; } = new(
        "stupefied",
        "Stupefied",
        ModifierType.Status,
        [new(StatKind.Will), new(StatKind.Perception), new(StatKind.SpellAttack), new(StatKind.SpellDc)],
        PlayerCoreConditions);

    public static FixedCondition Fascinated { get; } = new(
        "fascinated",
        "Fascinated",
        ModifierType.Status,
        2,
        [new(StatKind.Perception), new(StatKind.Skill)],
        PlayerCoreConditions);

    public static FixedCondition Fatigued { get; } = new(
        "fatigued",
        "Fatigued",
        ModifierType.Status,
        1,
        [new(StatKind.ArmorClass), new(StatKind.Fortitude), new(StatKind.Reflex), new(StatKind.Will)],
        PlayerCoreConditions);

    public static FixedCondition Deafened { get; } =
        new("deafened", "Deafened", ModifierType.Status, 2, [new(StatKind.Perception)], PlayerCoreConditions);

    public static FixedCondition Blinded { get; } =
        new("blinded", "Blinded", ModifierType.Status, 4, [new(StatKind.Perception)], PlayerCoreConditions);

    public static FixedCondition Unconscious { get; } = new(
        "unconscious",
        "Unconscious",
        ModifierType.Status,
        4,
        [new(StatKind.ArmorClass), new(StatKind.Perception), new(StatKind.Reflex)],
        PlayerCoreConditions);

    public static FixedCondition OffGuard { get; } =
        new("off-guard", "Off-Guard", ModifierType.Circumstance, 2, [new(StatKind.ArmorClass)], PlayerCoreConditions);

    public static FixedCondition Prone { get; } =
        new("prone", "Prone", ModifierType.Circumstance, 2, [new(StatKind.Attack)], PlayerCoreConditions);

    public static FixedCondition Encumbered { get; } =
        new("encumbered", "Encumbered", ModifierType.Status, 1, ClumsyTargets, PlayerCoreConditions);

    public static ImmutableArray<ConditionDefinition> All { get; } =
    [
        Clumsy,
        Drained,
        Enfeebled,
        Frightened,
        Sickened,
        Stupefied,
        Fascinated,
        Fatigued,
        Deafened,
        Blinded,
        Unconscious,
        OffGuard,
        Prone,
        Encumbered,
    ];
}
