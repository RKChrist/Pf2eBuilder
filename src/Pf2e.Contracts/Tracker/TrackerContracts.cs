using Pf2e.Contracts.Rules;

namespace Pf2e.Contracts.Tracker;

/// <summary>Kind is "Exactly", "Governed", "AllChecksAndDcs", "SavingThrows" or "Speeds". Stat
/// is read only by "Exactly" and Attribute only by "Governed".</summary>
public sealed record SelectorSpecView(string Kind, string? Stat, string? Attribute, string? SkillName);

public sealed record EffectModifierView(string Type, int Value, IReadOnlyList<SelectorSpecView> Applies);

/// <summary>Kind is "Seeded", "Rule" or "Custom". Key is a registry key or a rule id, and is
/// null for a custom effect.</summary>
public sealed record EffectSpec(
    string Name,
    string Kind,
    string? Key,
    int Value,
    string? Duration,
    IReadOnlyList<EffectModifierView> Modifiers);

public sealed record ActiveEffectView(
    Guid Id,
    string Name,
    string Kind,
    string? Key,
    int Value,
    bool HasValue,
    string? Duration,
    IReadOnlyList<EffectModifierView> Modifiers);

public sealed record CharacterSheetView(
    Guid Id,
    string Name,
    int Level,
    string ClassName,
    string AncestryName,
    BreakdownSummary ArmorClass,
    BreakdownSummary Fortitude,
    BreakdownSummary Reflex,
    BreakdownSummary Will,
    BreakdownSummary Perception,
    BreakdownSummary ClassDc,
    int CurrentHitPoints,
    int MaxHitPoints,
    int TemporaryHitPoints,
    int HeroPoints,
    IReadOnlyList<ActiveEffectView> Effects,
    IReadOnlyList<NamedBreakdownView> Skills,
    IReadOnlyList<NamedBreakdownView> Attacks,
    BreakdownSummary? SpellAttack,
    BreakdownSummary? SpellDc,
    string? SpellTradition);

/// <summary>A computed number with a name of its own rather than a slot on the sheet: one skill,
/// one weapon. Rank is the skill's proficiency by name and null for a weapon.</summary>
public sealed record NamedBreakdownView(string Name, BreakdownSummary Value, string? Rank);

/// <summary>Exists is false for a code nobody has imported into yet. Anyone with a code is at
/// that table, so an unknown code is a table waiting to be started rather than an error; the
/// screen still needs to tell the two apart so a mistyped code does not look like a join.</summary>
public sealed record TableView(string Code, bool Exists, IReadOnlyList<CharacterSheetView> Characters);

public sealed record ImportCharacterRequest(string Pathbuilder);

public sealed record ChangeHitPointsRequest(int Delta);

/// <summary>A null Effect removes whatever is in that slot.</summary>
public sealed record SetEffectRequest(EffectSpec? Effect);
