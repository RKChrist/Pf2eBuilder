using Pf2e.Contracts.Tracker;

namespace Pf2e.Contracts.Rules;

/// <summary>
/// What a list or picker shows. Deliberately not the whole record.
/// <see cref="HasModifiers"/> lets the effect picker say honestly that a record states nothing
/// this app can apply yet, rather than offering a row that does nothing when tapped.
/// </summary>
public sealed record RuleSummary(
    string Id,
    string Category,
    string Name,
    int? Level,
    string? Rarity,
    string? PrimarySource,
    IReadOnlyList<string> Traits,
    string SourceUrl,
    bool HasModifiers);

public sealed record RuleSearchResult(
    IReadOnlyList<RuleSummary> Items,
    int TotalMatching,
    int Page,
    int PageSize);

/// <summary>
/// A condition the engine can compute, paired with where its rule is printed. The modifiers
/// come from the engine because Archives of Nethys publishes a condition's text but not its
/// structured effect; the link comes from the seeded record.
/// </summary>
public sealed record ConditionSummary(
    string Key,
    string Name,
    bool HasValue,
    bool Verified,
    IReadOnlyList<ModifierSummary> Modifiers,
    string? SourceUrl);

public sealed record ModifierSummary(string Source, string Type, int Value, IReadOnlyList<string> Applies);

/// <summary>Every contributing part of a computed number, so the sheet can explain itself.</summary>
public sealed record BreakdownSummary(
    int Base,
    int Total,
    IReadOnlyList<ModifierSummary> Applied,
    IReadOnlyList<SuppressedSummary> Suppressed);

public sealed record SuppressedSummary(ModifierSummary Modifier, string Reason);

/// <summary>Mechanics arrive flattened, so a client never reimplements the seed's JSON schema.
/// Modifiers are the subset a tracker can apply, extracted at ingest.</summary>
public sealed record RuleDetail(
    RuleSummary Summary,
    IReadOnlyList<MechanicField> Mechanics,
    IReadOnlyList<EffectModifierView> Modifiers);

/// <summary>A scalar becomes one value; a JSON array becomes one value per element.</summary>
public sealed record MechanicField(string Key, IReadOnlyList<string> Values);
