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
    Guid? SourceCreatureId = null,
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
    Guid? SourceCreatureId,
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
/// A monster's hit points and stat line. This type exists so that "a player never sees these"
/// is one nullable field on one view rather than seven fields a projection has to remember to
/// blank. A player projection never populates it, at any reveal state.
/// </summary>
public sealed record MonsterStatLineView(
    int CurrentHitPoints,
    int MaxHitPoints,
    int TemporaryHitPoints,
    int Level,
    int ArmorClass,
    int Fortitude,
    int Reflex,
    int Will,
    int Perception,
    string RuleId,
    IReadOnlyList<string> Traits);

/// <summary>
/// One creature in the initiative order. Kind is "PlayerCharacter" or "Adversary".
/// <para>A player character's hit points are not here: they are on the character sheet in the
/// same payload, which is the one place they live. Monster is null for a player character and
/// for every monster in a player's projection.</para>
/// </summary>
public sealed record CombatantView(
    Guid Id,
    string Kind,
    string Name,
    int Initiative,
    bool IsCurrentTurn,
    bool Revealed,
    IReadOnlyList<ActiveEffectView> Effects,
    MonsterStatLineView? Monster);

/// <summary>
/// Round is zero before initiative is rolled. Reminders are what the last turn change left for
/// the DM to do; a reminder about a creature the viewer cannot see is not in their copy.
/// </summary>
public sealed record EncounterView(
    int Round,
    Guid? CurrentCombatantId,
    IReadOnlyList<CombatantView> Combatants,
    IReadOnlyList<string> Reminders);

/// <summary>
/// Mode is "Exploration", "Encounter" or "Downtime"; Role is "Player" or "Dm". Role is here so
/// the screen can offer the DM's controls, and not so it can decide what to hide: anything a
/// player may not see has already been left out of this value by the projection.
/// </summary>
public sealed record CampaignView(
    string Code,
    string Mode,
    string Role,
    IReadOnlyList<CharacterSheetView> Characters,
    IReadOnlyList<EffectApplicationView> Effects,
    EncounterView? Encounter);

/// <summary>The one payload that ever carries the DM key. Whoever asked for it is the DM, and
/// no later response repeats it.</summary>
public sealed record CreatedCampaignView(string Code, string DmKey, string Mode);

public sealed record CampaignModeView(string Code, string Mode);

/// <summary>Mode is "Exploration", "Encounter" or "Downtime".</summary>
public sealed record SetModeRequest(string Mode);

public sealed record ImportCharacterRequest(string Pathbuilder);

/// <summary>An amount and a direction, so 37 points of damage is one request. Direction is
/// "Damage" or "Heal"; the signed delta the database adds is derived from it, because two
/// people damaging one creature at once have to sum.</summary>
public sealed record ChangeHitPointsRequest(int Amount, string Direction);

/// <summary>Name a creature record to add a monster, or a character to add somebody from the
/// roster. Name is an optional label, so "Ogre 2" is a name the DM can read.</summary>
public sealed record AddCombatantRequest(string? RuleId, Guid? CharacterId, string? Name, int? Initiative);

public sealed record InitiativeRoll(Guid CombatantId, int Initiative);

/// <summary>A combatant the rolls do not name has its initiative rolled by the server.</summary>
public sealed record RollInitiativeRequest(IReadOnlyList<InitiativeRoll> Rolls);

public sealed record RevealMonsterRequest(bool Revealed);

/// <summary>
/// The fields that feed the calculator, in the form the screen edits them. Everything here is
/// the build layer; the session layer is not in this value and an edit does not touch it.
/// Ranks are "Untrained", "Trained", "Expert", "Master" or "Legendary".
/// </summary>
public sealed record CharacterBuildEdit(
    string Name,
    int Level,
    string ClassName,
    string AncestryName,
    string KeyAttribute,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string Fortitude,
    string Reflex,
    string Will,
    string Perception,
    string ClassDc,
    string ArmorRank,
    string ArmorName,
    int ArmorItemBonus,
    int? ArmorDexCap,
    int AncestryHitPoints,
    int ClassHitPoints,
    int BonusHitPoints,
    int BonusHitPointsPerLevel);

