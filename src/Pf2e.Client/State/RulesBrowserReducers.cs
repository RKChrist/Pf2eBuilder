using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

public static class RulesBrowserReducers
{
    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, GroupSelected action) =>
        Unfiltered(state) with { ActiveGroup = action.Group, ActiveCategory = null };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, CategorySelected action) =>
        Unfiltered(state) with { ActiveCategory = action.Category };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, CategoryCleared _) =>
        Unfiltered(state) with { ActiveCategory = null };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, QueryChanged action) =>
        state with { Query = action.Query, Page = 1 };

    /// <summary>
    /// The API rejects a range whose floor is above its ceiling. Ordering the two picks here
    /// means a player who sets "from 10" after "to 2" gets levels 2 to 10 rather than an error
    /// about a request they did not knowingly make.
    /// </summary>
    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, LevelRangeChanged action)
    {
        var (min, max) = (action.MinLevel, action.MaxLevel) switch
        {
            (int low, int high) when low > high => (high, low),
            var picked => picked,
        };

        return state with { MinLevel = min, MaxLevel = max, Page = 1 };
    }

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, TraitSelected action) =>
        state with { Trait = action.Trait, Page = 1 };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, PageSelected action) =>
        state with { Page = action.Page };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchStarted _) =>
        state with { Results = new RemoteData<RuleSearchResult>.Loading() };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchSucceeded action) =>
        state with { Results = new RemoteData<RuleSearchResult>.Loaded(action.Result) };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchFailed action) =>
        state with { Results = new RemoteData<RuleSearchResult>.Failed(action.Message) };

    /// <summary>Carrying a trait from feats into spells shows an empty list and no reason for it.</summary>
    static RulesBrowserState Unfiltered(RulesBrowserState state) => state with
    {
        Query = string.Empty,
        MinLevel = null,
        MaxLevel = null,
        Trait = null,
        Page = 1,
        Results = new RemoteData<RuleSearchResult>.NotAsked(),
    };
}
