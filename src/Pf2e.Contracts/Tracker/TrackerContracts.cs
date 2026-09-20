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

/// <summary>
/// Who an effect is being applied to. Kind is "Character" or "Monster", which name one creature
/// through Id, or "AllPlayerCharacters" or "AllMonsters", which name a group and carry no Id.
/// <para>"All player characters" is the campaign's whole roster and not only the ones in the
/// fight, because Rallying Anthem reaches the party whether or not initiative has been rolled.</para>
/// </summary>
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
    IReadOnlyList<ActiveEffectView> Effects,
    IReadOnlyList<NamedBreakdownView> Skills,
    IReadOnlyList<NamedBreakdownView> Attacks,
    BreakdownSummary? SpellAttack,
    BreakdownSummary? SpellDc,
    string? SpellTradition,
    // The values the totals above are computed from, in exactly the shape an edit sends back.
    // Without this the edit screen has nothing to open with: a sheet states Fortitude +11 and
    // an edit states Trained, and one cannot be worked back from the other.
    CharacterBuildEdit Build,
    /// <summary>What this character is doing while the party travels, by key, and null when
    /// nobody has said. Session state: a level-up does not stop them scouting.</summary>
    string? ExplorationActivity,
    /// <summary>Minutes left on this character's Treat Wounds immunity, and zero when there is
    /// none. Computed against the campaign clock so a screen never has to subtract.</summary>
    int TreatWoundsImmuneFor,
    /// <summary>What this character is spending today on, by key, and null when nobody has said.</summary>
    string? DowntimeActivity,
    /// <summary>The level of the task they took on, and null where the activity has none.</summary>
    int? DowntimeTaskLevel,
    /// <summary>The DC that task level comes to, so no screen carries the table. Null when no
    /// task level was set.</summary>
    int? DowntimeDc,
    /// <summary>The feats and spells off this character's own sheet. RuleId is the seeded
    /// record where the ruleset holds one, and null for homebrew or a name newer than the
    /// snapshot, which still shows and simply does not open.</summary>
    IReadOnlyList<SheetEntryView> Feats,
    IReadOnlyList<SheetEntryView> Spells,
    /// <summary>What this character can attempt and what they cannot, computed from their own
    /// ranks. The server does the deciding so no screen holds a copy of the rules.</summary>
    IReadOnlyList<AttemptView> CanAttempt);

/// <summary>
/// One skill action as it stands for one character. <paramref name="Skill"/> is the one they
/// would actually roll, which is the best of the ones the action allows, and
/// <paramref name="Modifier"/> is their total in it with every condition and buff already on
/// it. <paramref name="Met"/> is false when they are not trained enough to try.
/// </summary>
public sealed record AttemptView(
    string Key, string Name, string Skill, int Modifier, bool Met, string Required, string When);

public sealed record SheetEntryView(string Name, string Kind, int Level, string? RuleId);

/// <summary>One thing a character can be doing between fights, with the sentence a screen
/// shows under it. The list is the engine's, so a screen never spells one out itself.</summary>
public sealed record ExplorationActivityView(string Key, string Name, string Consequence);

public sealed record SetExplorationActivityRequest(string? Activity);

/// <summary>One ten-minute activity, with what it costs and what it does.</summary>
public sealed record CampActivityView(string Key, string Name, int Minutes, string What);

public sealed record CampActivityRequest(string Activity);

/// <summary>
/// One activity from the camping rules, as the ruleset holds it.
/// <para><paramref name="Requires"/> is the sentence the record states, and null where it
/// states none. <paramref name="Rank"/> and <paramref name="Skills"/> are that sentence read
/// back, and are null where it is not a plain proficiency phrase: "knowledge of the recipe" is
/// a real requirement and not one a sheet can check.</para>
/// </summary>
public sealed record CampingActivityView(
    string RuleId,
    string Name,
    string? Requires,
    string? Rank,
    IReadOnlyList<string> Skills);

/// <summary>One thing a day can be spent on. Skill is the one it is rolled with and null
/// where the activity has no single skill.</summary>
public sealed record DowntimeActivityView(string Key, string Name, string? Skill, string What);

public sealed record SetDowntimeActivityRequest(string? Activity, int? TaskLevel);

/// <summary>A computed number with a name of its own rather than a slot on the sheet: one skill,
/// one weapon. Rank is the skill's proficiency by name and null for a weapon.</summary>
public sealed record NamedBreakdownView(string Name, BreakdownSummary Value, string? Rank);

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
    EncounterView? Encounter,
    /// <summary>How long the party has been at this, in minutes. Only ever goes up.</summary>
    int ElapsedMinutes,
    /// <summary>Which downtime day the party is on, counting from one.</summary>
    int Day);

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

/// <summary>Correcting one number, which is not the same command as rolling: rolling gives
/// everybody a number and starts the round, and doing that to fix a misheard twelve would
/// re-roll the whole fight.</summary>
public sealed record SetInitiativeRequest(int Initiative);

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

