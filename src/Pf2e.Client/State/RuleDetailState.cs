using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

[FeatureState]
public sealed record RuleDetailState
{
    /// <summary>Null means no sheet is open, which is what a back navigation restores.</summary>
    public string? Id { get; init; }

    public RemoteData<RuleDetail> Detail { get; init; } = new RemoteData<RuleDetail>.NotAsked();
}

public sealed record RuleOpened(string Id);

public sealed record RuleClosed;

public sealed record RuleRetried;

public sealed record RuleLoaded(RuleDetail Detail);

public sealed record RuleFailed(string Message);

public static class RuleDetailReducers
{
    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleOpened action) =>
        state with { Id = action.Id, Detail = new RemoteData<RuleDetail>.Loading() };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleRetried _) =>
        state with { Detail = new RemoteData<RuleDetail>.Loading() };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleClosed _) =>
        state with { Id = null, Detail = new RemoteData<RuleDetail>.NotAsked() };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleLoaded action) =>
        state with { Detail = new RemoteData<RuleDetail>.Loaded(action.Detail) };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleFailed action) =>
        state with { Detail = new RemoteData<RuleDetail>.Failed(action.Message) };
}
