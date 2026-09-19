namespace Pf2e.Contracts.Rules;

/// <summary>What a list or picker shows. Deliberately not the whole record.</summary>
public sealed record RuleSummary(
    string Id,
    string Category,
    string Name,
    int? Level,
    string? Rarity,
    string? PrimarySource,
    IReadOnlyList<string> Traits,
    string SourceUrl);

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

/// <summary>
/// One record with the sparse per-category fields a list deliberately leaves out. The mechanics
/// arrive already flattened to strings, because their JSON shape differs per category and a
/// client that had to parse it would be reimplementing the seed's schema.
/// </summary>
public sealed record RuleDetail(RuleSummary Summary, IReadOnlyList<MechanicField> Mechanics);

/// <summary>A scalar becomes one value; a JSON array becomes one value per element.</summary>
public sealed record MechanicField(string Key, IReadOnlyList<string> Values);
