using Fluxor;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

[FeatureState]
public sealed record RuleDetailState
{
    /// <summary>Null means no sheet is open, which is what a back navigation restores.</summary>
    public string? Id { get; init; }

    public RemoteData<RuleDetail> Detail { get; init; } = new RemoteData<RuleDetail>.NotAsked();

    /// <summary>The record's own words, asked for beside the mechanics and not as part of them:
    /// they come from further away, and the numbers should not wait for the sentences.</summary>
    public RemoteData<RuleText> Text { get; init; } = new RemoteData<RuleText>.NotAsked();
}

public sealed record RuleOpened(string Id);

public sealed record RuleClosed;

public sealed record RuleRetried;

public sealed record RuleLoaded(RuleDetail Detail);

public sealed record RuleFailed(string Message);

public sealed record RuleMissing;

/// <summary>Carries the id it was asked for, because a slow answer can land after the reader has
/// opened something else.</summary>
public sealed record RuleTextLoaded(string Id, RuleText Text);

public sealed record RuleTextFailed(string Id);

public static class RuleDetailReducers
{
    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleOpened action) =>
        state with
        {
            Id = action.Id,
            Detail = new RemoteData<RuleDetail>.Loading(),
            Text = new RemoteData<RuleText>.Loading(),
        };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleRetried _) =>
        state with { Detail = new RemoteData<RuleDetail>.Loading() };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleClosed _) =>
        state with
        {
            Id = null,
            Detail = new RemoteData<RuleDetail>.NotAsked(),
            Text = new RemoteData<RuleText>.NotAsked(),
        };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleTextLoaded action) =>
        state.Id == action.Id ? state with { Text = new RemoteData<RuleText>.Loaded(action.Text) } : state;

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleTextFailed action) =>
        state.Id == action.Id ? state with { Text = new RemoteData<RuleText>.Missing() } : state;

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleLoaded action) =>
        state with { Detail = new RemoteData<RuleDetail>.Loaded(action.Detail) };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleMissing _) =>
        state with { Detail = new RemoteData<RuleDetail>.Missing() };

    [ReducerMethod]
    public static RuleDetailState On(RuleDetailState state, RuleFailed action) =>
        state with { Detail = new RemoteData<RuleDetail>.Failed(action.Message) };
}
