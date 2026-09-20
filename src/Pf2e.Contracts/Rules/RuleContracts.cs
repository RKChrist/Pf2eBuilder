using Pf2e.Contracts.Tracker;

namespace Pf2e.Contracts.Rules;

/// <summary>
/// What a list or picker shows. Deliberately not the whole record.
/// <see cref="Highlights"/> is the few facts a row can say at a glance, chosen per category
/// by the server. <see cref="HasModifiers"/> lets the effect picker say honestly that a
/// record states nothing this app can apply yet, rather than offering a row that does
/// nothing when tapped.
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
    IReadOnlyList<MechanicField> Highlights,
    bool HasModifiers);

public sealed record RuleSearchResult(
    IReadOnlyList<RuleSummary> Items,
    int TotalMatching,
    int Page,
    int PageSize);

/// <summary>How many records match in each category that has any, so a screen can say how much
/// is behind a door before anyone opens it.</summary>
public sealed record RuleCounts(int Total, IReadOnlyList<CategoryCount> Categories);

/// <summary>The lowest and highest level in the category, over the same filter as the count, and
/// null where no record in it has a level at all.</summary>
public sealed record CategoryCount(string Category, int Count, int? LowestLevel = null, int? HighestLevel = null);

/// <summary>How many records in one category carry each trait, most common first, so a filter can
/// offer the traits that matter there rather than the ones that happen to be on screen.</summary>
public sealed record TraitCounts(IReadOnlyList<TraitCount> Traits);

/// <summary><paramref name="Group"/> is the kind of trait, such as Class or Ancestry, from the trait's
/// own record, and null for a trait the ruleset has no record of.</summary>
public sealed record TraitCount(string Trait, int Count, string? Group);

/// <summary>
/// A condition the engine can compute, paired with where its rule is printed. The modifiers
/// come from the engine because Archives of Nethys publishes a condition's text but not its
/// structured effect; the link comes from the seeded record.
/// </summary>
public sealed record ConditionSummary(
    string Key,
    string Name,
    /// <summary>"Condition" or "Buff". A picker that offers a raised shield beside frightened
    /// without saying which is which makes the table read every row to find one.</summary>
    string Kind,
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
    IReadOnlyList<SuppressedSummary> Suppressed,
    /// <summary>What the number would be with only what the character is carrying: no condition,
    /// no buff, nothing anybody did to them.</summary>
    int Bare)
{
    /// <summary>How far the effects on this creature have moved the number, and zero when
    /// nothing is moving it.
    /// <para>Not the sum of the applied effect modifiers. An anthem's status bonus can suppress
    /// a worn one, and taking the anthem off gives the worn bonus back rather than losing both,
    /// so a screen that summed the list would say the anthem is worth more than it is.</para>
    /// </summary>
    public int Swing => Total - Bare;
}

public sealed record SuppressedSummary(ModifierSummary Modifier, string Reason);

/// <summary>Mechanics arrive flattened, so a client never reimplements the seed's JSON schema.
/// Modifiers are the subset a tracker can apply, extracted at ingest.
/// <see cref="Links"/> names every value that is another record's exact name, so a client
/// links only what exists.</summary>
public sealed record RuleDetail(
    RuleSummary Summary,
    IReadOnlyList<MechanicField> Mechanics,
    IReadOnlyList<EffectModifierView> Modifiers,
    IReadOnlyList<RuleLink> Links);

/// <summary>The value <paramref name="Value"/> of field <paramref name="Field"/> is the record <paramref name="Id"/>.</summary>
public sealed record RuleLink(string Field, string Value, string Id);

/// <summary>A scalar becomes one value; a JSON array becomes one value per element.</summary>
public sealed record MechanicField(string Key, IReadOnlyList<string> Values);
