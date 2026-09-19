using Fluxor;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

/// <summary>How many records every category holds. The ruleset does not change while the app is
/// open, so this is asked once and kept.</summary>
[FeatureState]
public sealed record CatalogSizeState
{
    public RemoteData<RuleCounts> Sizes { get; init; } = new RemoteData<RuleCounts>.NotAsked();

    public int? Of(string category) => Sizes is RemoteData<RuleCounts>.Loaded loaded
        ? loaded.Value.Categories.FirstOrDefault(c => c.Category == category)?.Count ?? 0
        : null;
}

public sealed record CatalogSizesRequested;

public sealed record CatalogSizesLoaded(RuleCounts Sizes);

public sealed record CatalogSizesFailed(string Message);

public static class CatalogSizeReducers
{
    [ReducerMethod]
    public static CatalogSizeState On(CatalogSizeState state, CatalogSizesRequested _) =>
        state with { Sizes = new RemoteData<RuleCounts>.Loading() };

    [ReducerMethod]
    public static CatalogSizeState On(CatalogSizeState state, CatalogSizesLoaded action) =>
        state with { Sizes = new RemoteData<RuleCounts>.Loaded(action.Sizes) };

    [ReducerMethod]
    public static CatalogSizeState On(CatalogSizeState state, CatalogSizesFailed action) =>
        state with { Sizes = new RemoteData<RuleCounts>.Failed(action.Message) };
}

public sealed class CatalogSizeEffects(RulesApi api)
{
    [EffectMethod]
    public async Task Handle(CatalogSizesRequested _, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new CatalogSizesLoaded(await api.CountAsync(null, null, CancellationToken.None)));
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new CatalogSizesFailed(failure.Message));
        }
    }
}
