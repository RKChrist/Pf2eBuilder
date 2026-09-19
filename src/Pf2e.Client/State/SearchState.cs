using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>
/// Search across every category, shared by the field in the header and the results screen, so
/// typing in one is what the other shows. <see cref="Scope"/> narrows the results to one
/// category; the counts never narrow, because they are what the reader picks a scope from.
/// </summary>
[FeatureState]
public sealed record SearchState
{
    public const int PageSize = 20;

    public string Query { get; init; } = string.Empty;

    public string? Scope { get; init; }

    public int Page { get; init; } = 1;

    public RemoteData<RuleSearchResult> Results { get; init; } = new RemoteData<RuleSearchResult>.NotAsked();

    public RemoteData<RuleCounts> Counts { get; init; } = new RemoteData<RuleCounts>.NotAsked();

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
}

/// <summary>A keystroke. Debounced, and it widens the scope back to everything.</summary>
public sealed record SearchTyped(string Query);

/// <summary>An address such as /search?q=shield&amp;in=feat, applied at once.</summary>
public sealed record SearchOpened(string Query, string? Scope, int Page);

public sealed record SearchRetriedEverywhere;

public sealed record SearchLoading;

public sealed record SearchAnswered(RuleSearchResult Results, RuleCounts Counts);

public sealed record SearchBroke(string Message);

public static class SearchReducers
{
    [ReducerMethod]
    public static SearchState On(SearchState state, SearchTyped action) =>
        Asked(state, action.Query) with { Scope = null, Page = 1 };

    [ReducerMethod]
    public static SearchState On(SearchState state, SearchOpened action) =>
        Asked(state, action.Query) with { Scope = action.Scope, Page = Math.Max(1, action.Page) };

    /// <summary>Counts depend on the words alone, so a new scope or page keeps the ones it has
    /// and the chips a reader is choosing between do not blink out under the finger.</summary>
    [ReducerMethod]
    public static SearchState On(SearchState state, SearchLoading _) => state with
    {
        Results = new RemoteData<RuleSearchResult>.Loading(),
        Counts = state.Counts is RemoteData<RuleCounts>.Loaded ? state.Counts : new RemoteData<RuleCounts>.Loading(),
    };

    [ReducerMethod]
    public static SearchState On(SearchState state, SearchAnswered action) => state with
    {
        Results = new RemoteData<RuleSearchResult>.Loaded(action.Results),
        Counts = new RemoteData<RuleCounts>.Loaded(action.Counts),
    };

    [ReducerMethod]
    public static SearchState On(SearchState state, SearchBroke action) => state with
    {
        Results = new RemoteData<RuleSearchResult>.Failed(action.Message),
        Counts = new RemoteData<RuleCounts>.Failed(action.Message),
    };

    /// <summary>New words forget the old counts. An empty field asks nothing, so it shows nothing
    /// rather than the last answer.</summary>
    static SearchState Asked(SearchState state, string query)
    {
        var next = state with { Query = query };
        if (query != state.Query)
        {
            next = next with { Counts = new RemoteData<RuleCounts>.NotAsked() };
        }

        return next.HasQuery ? next : next with { Results = new RemoteData<RuleSearchResult>.NotAsked() };
    }
}
