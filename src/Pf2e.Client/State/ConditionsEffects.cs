using Fluxor;
using Pf2e.Client.Api;

namespace Pf2e.Client.State;

public sealed class ConditionsEffects(RulesApi api)
{
    [EffectMethod]
    public async Task Handle(ConditionsRequested _, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new ConditionsLoaded(await api.GetConditionsAsync(CancellationToken.None)));
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new ConditionsFailed(failure.Message));
        }
    }
}
