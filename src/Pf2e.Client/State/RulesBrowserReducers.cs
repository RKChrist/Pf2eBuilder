using Fluxor;
using Pf2e.Client.Catalog;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

public static class RulesBrowserReducers
{
    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, BoardOpened action) => state with
    {
        ActiveGroup = action.Group,
        Address = null,
        Results = new RemoteData<RuleSearchResult>.NotAsked(),
        Traits = new RemoteData<TraitCounts>.NotAsked(),
    };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, GroupEntered action) =>
        state with { ActiveGroup = action.Group };

    /// <summary>Another category forgets the last one's list and traits, so feats never show
    /// under a spells heading while the spells load.</summary>
    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, CategoryAddressed action) =>
        state.Address?.Category == action.Address.Category
            ? state with { Address = action.Address }
            : state with
            {
                ActiveGroup = RuleCatalog.Of(action.Address.Category)?.Group ?? state.ActiveGroup,
                Address = action.Address,
                Results = new RemoteData<RuleSearchResult>.NotAsked(),
                Traits = new RemoteData<TraitCounts>.NotAsked(),
            };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchStarted _) =>
        state with { Results = new RemoteData<RuleSearchResult>.Loading() };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchSucceeded action) =>
        state with { Results = new RemoteData<RuleSearchResult>.Loaded(action.Result) };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, SearchFailed action) =>
        state with { Results = new RemoteData<RuleSearchResult>.Failed(action.Message) };

    /// <summary>The traits already shown stay while new counts load, so the chips a reader is
    /// choosing between do not blink out under the finger.</summary>
    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, TraitCountsStarted _) =>
        state.Traits is RemoteData<TraitCounts>.Loaded ? state : state with { Traits = new RemoteData<TraitCounts>.Loading() };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, TraitCountsLoaded action) =>
        state with { Traits = new RemoteData<TraitCounts>.Loaded(action.Traits) };

    [ReducerMethod]
    public static RulesBrowserState On(RulesBrowserState state, TraitCountsFailed action) =>
        state with { Traits = new RemoteData<TraitCounts>.Failed(action.Message) };
}
