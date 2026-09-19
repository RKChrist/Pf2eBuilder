using Pf2e.Contracts.Rules;

namespace Pf2e.Contracts.Tracker;

/// <summary>Kind is "Exactly", "Governed", "AllChecksAndDcs", "SavingThrows" or "Speeds". Stat
/// is read only by "Exactly" and Attribute only by "Governed".</summary>
public sealed record SelectorSpecView(string Kind, string? Stat, string? Attribute, string? SkillName);

public sealed record EffectModifierView(string Type, int Value, IReadOnlyList<SelectorSpecView> Applies);

/// <summary>
/// Kind is "Seeded", "Rule" or "Custom". Key is a registry key or a rule id, and is null for a
/// custom effect. Duration is the label a human reads; Timing and Rounds are the clock, and an
/// effect with no Timing has none. PersistentDamage is surfaced as a reminder at the end of the
/// affected creature's turn rather than applied, because the flat check that ends it is a roll.
/// </summary>
public sealed record EffectSpec(
    string Name,
    string Kind,
    string? Key,
    int Value,
    string? Duration,
    IReadOnlyList<EffectModifierView> Modifiers,
    string? Timing = null,
    int? Rounds = null,
    Guid? SourceCombatantId = null,
    int? PersistentDamage = null,
    string? PersistentDamageType = null);

/// <summary>Kind is "Character" for a named character. Id names the creature.</summary>
public sealed record EffectTargetSpec(string Kind, Guid? Id);

/// <summary>A null Effect removes the application this id names.</summary>
public sealed record ApplyEffectRequest(EffectSpec? Effect, IReadOnlyList<EffectTargetSpec> Targets);

/// <summary>Kind is "Character" or "Monster". Value and RemainingRounds are per creature,
/// because the duration clock and a scaling condition both move per creature: one party-wide
/// frightened 2 is one application and five countdowns.</summary>
public sealed record EffectTargetView(string Kind, Guid Id, int Value, int? RemainingRounds);

/// <summary>
/// One application of one effect and everything it reached. Rallying Anthem on the whole party
/// is this value with five targets, which is what lets a screen show it once above the party
/// instead of as five identical chips.
/// </summary>
public sealed record EffectApplicationView(
    Guid Id,
    string Name,
    string Kind,
    string? Key,
    bool HasValue,
    string? Duration,
    string Timing,
    Guid? SourceCombatantId,
    int? PersistentDamage,
    string? PersistentDamageType,
    IReadOnlyList<EffectModifierView> Modifiers,
    IReadOnlyList<EffectTargetView> Targets);

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
    IReadOnlyList<ActiveEffectView> Effects);

/// <summary>
/// Mode is "Exploration", "Encounter" or "Downtime"; Role is "Player" or "Dm". Role is here so
/// the screen can offer the DM's controls, and not so it can decide what to hide: anything a
/// player may not see has already been left out of this value by the projection.
/// </summary>
public sealed record CampaignView(
    string Code,
    string Mode,
    string Role,
    IReadOnlyList<CharacterSheetView> Characters);

/// <summary>The one payload that ever carries the DM key. Whoever asked for it is the DM, and
/// no later response repeats it.</summary>
public sealed record CreatedCampaignView(string Code, string DmKey, string Mode);

public sealed record CampaignModeView(string Code, string Mode);

/// <summary>Mode is "Exploration", "Encounter" or "Downtime".</summary>
public sealed record SetModeRequest(string Mode);

public sealed record ImportCharacterRequest(string Pathbuilder);

public sealed record ChangeHitPointsRequest(int Delta);

