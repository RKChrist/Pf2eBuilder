using Fluxor;
using Pf2e.Components;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>A null <see cref="ActiveCategory"/> is the browse screen's only mode switch: the
/// group's category list rather than any records.</summary>
[FeatureState]
public sealed record RulesBrowserState
{
    public GroupKey ActiveGroup { get; init; } = GroupKey.Build;

    public string? ActiveCategory { get; init; }

    public string Query { get; init; } = string.Empty;

    /// <summary>Whole scale means unfiltered. The two bounds cannot cross, so there is no
    /// arrangement of this filter that matches nothing.</summary>
    public IntRange Levels { get; init; } = LevelScale.Whole;

    public string? Trait { get; init; }

    public int Page { get; init; } = 1;

    public RemoteData<RuleSearchResult> Results { get; init; } = new RemoteData<RuleSearchResult>.NotAsked();
}

public sealed record GroupSelected(GroupKey Group);

/// <summary>Opens a category, optionally already narrowed to one trait, from wherever the reader
/// is: the group follows the category.</summary>
public sealed record CategorySelected(string Category, string? Trait = null);

public sealed record CategoryCleared;

public sealed record QueryChanged(string Query);

public sealed record LevelRangeChanged(IntRange Levels);

public sealed record TraitSelected(string? Trait);

public sealed record PageSelected(int Page);

public sealed record SearchRetried;

public sealed record SearchStarted;

public sealed record SearchSucceeded(RuleSearchResult Result);

public sealed record SearchFailed(string Message);
