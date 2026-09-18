using System.Collections.Immutable;

namespace Pf2e.Domain;

public enum StatKind
{
    ArmorClass,
    Fortitude,
    Reflex,
    Will,
    Perception,
    Attack,
    Damage,
    Skill,
    ClassDc,
    SpellAttack,
    SpellDc,
    Speed,
}

/// <summary>
/// The statistic a modifier applies to. A <see cref="StatKind.Skill"/> target with a null
/// <see cref="SkillName"/> means every skill.
/// </summary>
public readonly record struct StatTarget(StatKind Kind, string? SkillName = null)
{
    public static StatTarget Skill(string skillName) => new(StatKind.Skill, skillName);

    /// <summary>The kinds a condition means when its text says "all checks and DCs".</summary>
    public static ImmutableArray<StatTarget> AllChecks { get; } =
    [
        new(StatKind.Fortitude),
        new(StatKind.Reflex),
        new(StatKind.Will),
        new(StatKind.Perception),
        new(StatKind.Attack),
        new(StatKind.Skill),
        new(StatKind.SpellAttack),
        new(StatKind.ClassDc),
        new(StatKind.SpellDc),
    ];

    public bool Matches(StatTarget queried) =>
        Kind == queried.Kind && (SkillName is null || SkillName == queried.SkillName);
}
