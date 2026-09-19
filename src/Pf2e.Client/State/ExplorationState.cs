using Fluxor;
using Pf2e.Client.Api;
using Pf2e.Components;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

/// <summary>
/// The exploration activities and what each of them means.
/// <para>Fetched rather than compiled in. The list and the sentence under each choice are the
/// engine's, and the client does not reference the engine: it speaks to the API through the
/// contracts and nothing else. Copying nine strings into the client would be the second place
/// they live and the first place they go stale.</para>
/// </summary>
[FeatureState]
public sealed record ExplorationState
{
    public RemoteData<IReadOnlyList<ExplorationActivityView>> Activities { get; init; } =
        new RemoteData<IReadOnlyList<ExplorationActivityView>>.NotAsked();

    /// <summary>What the picker offers, with an empty key for "nothing in particular", because a
    /// character who has not chosen is the normal case and needs a way back to it.</summary>
    public IReadOnlyList<ChoiceOption<string>> Options =>
        Activities is RemoteData<IReadOnlyList<ExplorationActivityView>>.Loaded loaded
            ?
            [
                new(string.Empty, "Nothing in particular"),
                .. loaded.Value.Select(activity => new ChoiceOption<string>(activity.Key, activity.Name)),
            ]
            : [new(string.Empty, "Nothing in particular")];

    public string? ConsequenceOf(string? key) =>
        key is { Length: > 0 }
        && Activities is RemoteData<IReadOnlyList<ExplorationActivityView>>.Loaded loaded
            ? loaded.Value.FirstOrDefault(activity => activity.Key == key)?.Consequence
            : null;
}

public sealed record ExplorationActivitiesRequested;

public sealed record ExplorationActivitiesLoaded(IReadOnlyList<ExplorationActivityView> Activities);

public sealed record ExplorationActivitiesFailed(string Message);

public static class ExplorationReducers
{
    [ReducerMethod]
    public static ExplorationState On(ExplorationState state, ExplorationActivitiesRequested _) =>
        state with { Activities = new RemoteData<IReadOnlyList<ExplorationActivityView>>.Loading() };

    [ReducerMethod]
    public static ExplorationState On(ExplorationState state, ExplorationActivitiesLoaded action) =>
        state with { Activities = new RemoteData<IReadOnlyList<ExplorationActivityView>>.Loaded(action.Activities) };

    [ReducerMethod]
    public static ExplorationState On(ExplorationState state, ExplorationActivitiesFailed action) =>
        state with { Activities = new RemoteData<IReadOnlyList<ExplorationActivityView>>.Failed(action.Message) };
}

public sealed class ExplorationEffects(TrackerApi tracker)
{
    [EffectMethod]
    public async Task Handle(ExplorationActivitiesRequested _, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new ExplorationActivitiesLoaded(
                await tracker.GetExplorationActivitiesAsync(CancellationToken.None)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ExplorationActivitiesFailed(failure.Message));
        }
    }
}
