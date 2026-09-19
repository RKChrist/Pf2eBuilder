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

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, LevelRangeChanged action) =>
        state with { Levels = action.Levels, Page = 1 };

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
        Levels = LevelScale.Whole,
        Trait = null,
        Page = 1,
        Results = new RemoteData<RuleSearchResult>.NotAsked(),
    };
}
