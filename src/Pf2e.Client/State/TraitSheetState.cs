using Fluxor;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>What the app knows about one trait: its own record, when the ruleset has one by that
/// exact name, and every kind of record that carries it.</summary>
public sealed record TraitFacts(RuleSummary? Record, RuleCounts Usage);

[FeatureState]
public sealed record TraitSheetState
{
    /// <summary>Null means no trait sheet is open, which is what a back navigation restores.</summary>
    public string? Name { get; init; }

    public RemoteData<TraitFacts> Facts { get; init; } = new RemoteData<TraitFacts>.NotAsked();
}

public sealed record TraitOpened(string Name);

public sealed record TraitClosed;

public sealed record TraitRetried;

public sealed record TraitLoaded(string Name, TraitFacts Facts);

public sealed record TraitFailed(string Name, string Message);

public static class TraitSheetReducers
{
    [ReducerMethod]
    public static TraitSheetState On(TraitSheetState state, TraitOpened action) =>
        new() { Name = action.Name, Facts = new RemoteData<TraitFacts>.Loading() };

    [ReducerMethod]
    public static TraitSheetState On(TraitSheetState state, TraitRetried _) =>
        state with { Facts = new RemoteData<TraitFacts>.Loading() };

    [ReducerMethod]
    public static TraitSheetState On(TraitSheetState state, TraitClosed _) => new();

    /// <summary>An answer for a trait that is no longer open is dropped, so tapping two traits
    /// in quick succession never shows the first one's facts under the second one's name.</summary>
    [ReducerMethod]
    public static TraitSheetState On(TraitSheetState state, TraitLoaded action) =>
        action.Name == state.Name ? state with { Facts = new RemoteData<TraitFacts>.Loaded(action.Facts) } : state;

    [ReducerMethod]
    public static TraitSheetState On(TraitSheetState state, TraitFailed action) =>
        action.Name == state.Name ? state with { Facts = new RemoteData<TraitFacts>.Failed(action.Message) } : state;
}

public sealed class TraitSheetEffects(RulesApi api, IState<TraitSheetState> state)
{
    [EffectMethod]
    public Task Handle(TraitOpened action, IDispatcher dispatcher) => Load(action.Name, dispatcher);

    [EffectMethod]
    public Task Handle(TraitRetried _, IDispatcher dispatcher) =>
        state.Value.Name is { } name ? Load(name, dispatcher) : Task.CompletedTask;

    async Task Load(string name, IDispatcher dispatcher)
    {
        try
        {
            var record = api.FindAsync("trait", name, CancellationToken.None);
            var usage = api.CountAsync(null, name, CancellationToken.None);
            dispatcher.Dispatch(new TraitLoaded(name, new TraitFacts(await record, await usage)));
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new TraitFailed(name, failure.Message));
        }
    }
}
