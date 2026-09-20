using Fluxor;
using Pf2e.Client.Api;

namespace Pf2e.Client.State;

public sealed class RuleDetailEffects(RulesApi api, IState<RuleDetailState> state)
{
    [EffectMethod]
    public Task Handle(RuleOpened action, IDispatcher dispatcher) =>
        Task.WhenAll(Load(action.Id, dispatcher), LoadText(action.Id, dispatcher));

    /// <summary>A description that cannot be had is not a failure of the panel: the mechanics and
    /// the link out are still there, so this never raises the error state.</summary>
    async Task LoadText(string id, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(await api.GetRuleTextAsync(id, CancellationToken.None) is { } text
                ? (object)new RuleTextLoaded(id, text)
                : new RuleTextFailed(id));
        }
        catch (RulesApiException)
        {
            dispatcher.Dispatch(new RuleTextFailed(id));
        }
    }

    [EffectMethod]
    public Task Handle(RuleRetried _, IDispatcher dispatcher) => Load(state.Value.Id, dispatcher);

    async Task Load(string? id, IDispatcher dispatcher)
    {
        if (id is null)
        {
            return;
        }

        try
        {
            dispatcher.Dispatch(await api.GetRuleAsync(id, CancellationToken.None) is { } detail
                ? (object)new RuleLoaded(detail)
                : new RuleMissing());
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new RuleFailed(failure.Message));
        }
    }
}
