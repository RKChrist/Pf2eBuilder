using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>
/// A null <see cref="ActiveCategory"/> means the group's category list is showing rather than
/// any records, which is the browse screen's only mode switch.
/// </summary>
[FeatureState]
public sealed record RulesBrowserState
{
    public GroupKey ActiveGroup { get; init; } = GroupKey.Build;

    public string? ActiveCategory { get; init; }

    public string Query { get; init; } = string.Empty;

    public int? MinLevel { get; init; }

    public int? MaxLevel { get; init; }

    public string? Trait { get; init; }

    public int Page { get; init; } = 1;

    public RemoteData<RuleSearchResult> Results { get; init; } = new RemoteData<RuleSearchResult>.NotAsked();
}

public sealed record GroupSelected(GroupKey Group);

public sealed record CategorySelected(string Category);

public sealed record CategoryCleared;

public sealed record QueryChanged(string Query);

public sealed record LevelRangeChanged(int? MinLevel, int? MaxLevel);

public sealed record TraitSelected(string? Trait);

public sealed record PageSelected(int Page);

public sealed record SearchRetried;

/// <summary>Dispatched by the effect when the request actually leaves, not when the player types.</summary>
public sealed record SearchStarted;

public sealed record SearchSucceeded(RuleSearchResult Result);

public sealed record SearchFailed(string Message);
