using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

[FeatureState]
public sealed record ConditionsState
{
    public RemoteData<IReadOnlyList<ConditionSummary>> Conditions { get; init; } =
        new RemoteData<IReadOnlyList<ConditionSummary>>.NotAsked();
}

public sealed record ConditionsRequested;

public sealed record ConditionsLoaded(IReadOnlyList<ConditionSummary> Conditions);

public sealed record ConditionsFailed(string Message);

public static class ConditionsReducers
{
    [ReducerMethod]
    public static ConditionsState On(ConditionsState state, ConditionsRequested _) =>
        state with { Conditions = new RemoteData<IReadOnlyList<ConditionSummary>>.Loading() };

    [ReducerMethod]
    public static ConditionsState On(ConditionsState state, ConditionsLoaded action) =>
        state with { Conditions = new RemoteData<IReadOnlyList<ConditionSummary>>.Loaded(action.Conditions) };

    [ReducerMethod]
    public static ConditionsState On(ConditionsState state, ConditionsFailed action) =>
        state with { Conditions = new RemoteData<IReadOnlyList<ConditionSummary>>.Failed(action.Message) };
}
