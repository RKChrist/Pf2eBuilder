using Fluxor;
using Microsoft.Extensions.Options;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

public sealed class SearchEffects(RulesApi api, IState<SearchState> state, IOptions<ApiOptions> options)
{
    readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(options.Value.SearchDebounceMilliseconds);

    /// <summary>The same last-keystroke-wins cancellation as <see cref="RulesBrowserEffects"/>:
    /// a slow answer for "shi" must never land over a fast one for "shield".</summary>
    CancellationTokenSource? _pending;

    [EffectMethod]
    public Task Handle(SearchTyped _, IDispatcher dispatcher) => Search(_debounce, dispatcher);

    [EffectMethod]
    public Task Handle(SearchOpened _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    [EffectMethod]
    public Task Handle(SearchRetriedEverywhere _, IDispatcher dispatcher) => Search(TimeSpan.Zero, dispatcher);

    async Task Search(TimeSpan delay, IDispatcher dispatcher)
    {
        _pending?.Cancel();
        _pending?.Dispose();

        var mine = new CancellationTokenSource();
        _pending = mine;
        var ct = mine.Token;

        if (!state.Value.HasQuery)
        {
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            dispatcher.Dispatch(new SearchLoading());

            var now = state.Value;
            var words = now.Query.Trim();
            var results = api.SearchAsync(now.Scope, words, null, null, null, now.Page, ct, SearchState.PageSize);
            var counts = now.Counts is RemoteData<RuleCounts>.Loaded known
                ? Task.FromResult(known.Value)
                : api.CountAsync(words, null, ct);

            dispatcher.Dispatch(new SearchAnswered(await results, await counts));
        }
        catch (OperationCanceledException)
        {
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new SearchBroke(failure.Message));
        }
    }
}
