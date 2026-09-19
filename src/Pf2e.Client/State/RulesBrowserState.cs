using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>
/// Where the reader is in one category: everything a list screen's address says. The address
/// is the source and this is what it parses to, so refresh, back and a shared link all land
/// on the same list.
/// </summary>
public sealed record CategoryAddress(
    string Category,
    string Query = "",
    int? MinLevel = null,
    int? MaxLevel = null,
    string? Trait = null,
    int Page = 1)
{
    /// <summary>What the trait counts depend on. A trait or a page does not change them.</summary>
    public (string, string, int?, int?) FacetKey => (Category, Query, MinLevel, MaxLevel);

    public int FiltersSet => (MinLevel is null && MaxLevel is null ? 0 : 1) + (Trait is null ? 0 : 1);
}

/// <summary>A null <see cref="Address"/> is the board of <see cref="ActiveGroup"/>'s categories;
/// otherwise the screen is that category's records.</summary>
[FeatureState]
public sealed record RulesBrowserState
{
    public GroupKey ActiveGroup { get; init; } = GroupKey.Build;

    public CategoryAddress? Address { get; init; }

    public RemoteData<RuleSearchResult> Results { get; init; } = new RemoteData<RuleSearchResult>.NotAsked();

    public RemoteData<TraitCounts> Traits { get; init; } = new RemoteData<TraitCounts>.NotAsked();
}

/// <summary>The board's address was opened, such as / or /?group=feats.</summary>
public sealed record BoardOpened(GroupKey Group);

/// <summary>A screen of its own inside a group, such as conditions, marks that group current
/// without opening its board.</summary>
public sealed record GroupEntered(GroupKey Group);

/// <summary>A category's address was opened or changed, such as /browse/feat?with=Fighter.</summary>
public sealed record CategoryAddressed(CategoryAddress Address);

public sealed record SearchRetried;

public sealed record SearchStarted;

public sealed record SearchSucceeded(RuleSearchResult Result);

public sealed record SearchFailed(string Message);

public sealed record TraitCountsStarted;

public sealed record TraitCountsLoaded(TraitCounts Traits);

public sealed record TraitCountsFailed(string Message);
