using Fluxor;
using Microsoft.Extensions.Options;
using Pf2e.Client.Api;

namespace Pf2e.Client.State;

public sealed class RulesBrowserEffects(RulesApi api, IState<RulesBrowserState> state, IOptions<ApiOptions> options)
{
    readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(options.Value.SearchDebounceMilliseconds);

    /// <summary>
    /// Cancelling the previous search is what makes the last keystroke win. Without it a slow
    /// request for "shi" can land after a fast one for "shield" and overwrite it.
    /// </summary>
    CancellationTokenSource? _pending;

    [EffectMethod]
    public Task Handle(CategorySelected _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    [EffectMethod]
    public Task Handle(QueryChanged _, IDispatcher dispatcher) => Search(_debounce, dispatcher);

    [EffectMethod]
    public Task Handle(LevelRangeChanged _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    [EffectMethod]
    public Task Handle(TraitSelected _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    [EffectMethod]
    public Task Handle(PageSelected _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    [EffectMethod]
    public Task Handle(SearchRetried _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    async Task Search(TimeSpan delay, IDispatcher dispatcher)
    {
        _pending?.Cancel();
        _pending?.Dispose();

        var mine = new CancellationTokenSource();
        _pending = mine;
        var ct = mine.Token;

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            dispatcher.Dispatch(new SearchStarted());

            var now = state.Value;
            var result = await api.SearchAsync(
                now.ActiveCategory, now.Query, now.MinLevel, now.MaxLevel, now.Trait, now.Page, ct);

            dispatcher.Dispatch(new SearchSucceeded(result));
        }
        catch (OperationCanceledException)
        {
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new SearchFailed(failure.Message));
        }
    }
}
