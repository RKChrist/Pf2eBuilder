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

/// <summary>A null Effect removes whatever is in that slot.</summary>
public sealed record SetEffectRequest(EffectSpec? Effect);
