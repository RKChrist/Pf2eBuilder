using Fluxor;
using Pf2e.Client.Api;

namespace Pf2e.Client.State;

public sealed class RuleDetailEffects(RulesApi api, IState<RuleDetailState> state)
{
    [EffectMethod]
    public Task Handle(RuleOpened action, IDispatcher dispatcher) => Load(action.Id, dispatcher);

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
