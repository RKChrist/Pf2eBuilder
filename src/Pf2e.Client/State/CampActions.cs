using Fluxor;
using Pf2e.Client.Api;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

// A camping session, as the things somebody at the table does to it. Each is one request, and
// what comes back is the whole campaign, so there is no camp state here to keep in step with the
// server's: the page reads the camp off the campaign it was just handed.

public sealed record CampZoneSet(string? ZoneName, int ZoneDc, int EncounterDc);

/// <summary>How Prepare Campsite went, and null to take the result back.</summary>
public sealed record CampsiteRecorded(string? Outcome);

public sealed record CampingActivityTaken(Guid CharacterId, string Activity, string Outcome);

public sealed record CampEntrySaved(Guid EntryId, string Kind, string Name, string Does, int? Dc);

public sealed record CampEntryRemoved(Guid EntryId);

public sealed record MealChosen(Guid CharacterId, string? Kind, Guid? RecipeId, string? RuleId = null);

public sealed record CampSuppliesSet(int BasicIngredients, int SpecialIngredients);

public sealed record CampBroken;

public sealed class CampEffects(TrackerApi tracker, IState<CampaignState> state)
{
    [EffectMethod]
    public Task Handle(CampZoneSet action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Put, "zone",
            new SetCampZoneRequest(action.ZoneName, action.ZoneDc, action.EncounterDc));

    [EffectMethod]
    public Task Handle(CampsiteRecorded action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Put, "campsite", new RecordCampsiteRequest(action.Outcome));

    [EffectMethod]
    public Task Handle(CampingActivityTaken action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Post, $"characters/{action.CharacterId}/activities",
            new TakeCampingActivityRequest(action.Activity, action.Outcome));

    [EffectMethod]
    public Task Handle(CampEntrySaved action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Put, $"book/{action.EntryId}",
            new SaveCampEntryRequest(action.Kind, action.Name, action.Does, action.Dc));

    [EffectMethod]
    public Task Handle(CampEntryRemoved action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Delete, $"book/{action.EntryId}", null);

    [EffectMethod]
    public Task Handle(MealChosen action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Put, $"characters/{action.CharacterId}/meal",
            new ChooseMealRequest(action.Kind, action.RecipeId, action.RuleId));

    [EffectMethod]
    public Task Handle(CampSuppliesSet action, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Put, "supplies",
            new SetCampSuppliesRequest(action.BasicIngredients, action.SpecialIngredients));

    [EffectMethod]
    public Task Handle(CampBroken _, IDispatcher dispatcher) =>
        Run(dispatcher, HttpMethod.Post, "break", null);

    /// <summary>A refusal is a rule of camping saying no, and the sentence is the rule, so it goes
    /// where every other refused action on a campaign page goes.</summary>
    async Task Run(IDispatcher dispatcher, HttpMethod method, string path, object? body)
    {
        try
        {
            dispatcher.Dispatch(new CampaignRefreshed(
                await tracker.CampAsync(state.Value.Code, method, path, body, CancellationToken.None)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
        }
    }
}
