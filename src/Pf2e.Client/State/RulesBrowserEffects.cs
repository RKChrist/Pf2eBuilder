using Fluxor;
using Microsoft.Extensions.Options;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.State;

public sealed class RulesBrowserEffects(RulesApi api, IState<RulesBrowserState> state, IOptions<ApiOptions> options)
{
    readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(options.Value.SearchDebounceMilliseconds);

    /// <summary>
    /// Cancelling the previous request is what makes the last keystroke win. Without it a slow
    /// answer for "shi" can land after a fast one for "shield" and overwrite it.
    /// </summary>
    CancellationTokenSource? _search;
    CancellationTokenSource? _traits;

    CategoryAddress? _searched;
    CategoryAddress? _counted;

    [EffectMethod]
    public Task Handle(BoardOpened _, IDispatcher dispatcher)
    {
        Cancel(ref _search);
        Cancel(ref _traits);
        _searched = null;
        _counted = null;
        return Task.CompletedTask;
    }

    /// <summary>Typing in the name field changes only the query, and waits out the debounce; any
    /// other change to the address is a deliberate choice and asks at once.</summary>
    [EffectMethod]
    public Task Handle(CategoryAddressed action, IDispatcher dispatcher)
    {
        var address = action.Address;
        var typed = _searched is { } before && before.Query != address.Query && before with { Query = address.Query } == address;
        var delay = typed ? _debounce : TimeSpan.Zero;
        _searched = address;

        var work = new List<Task> { Search(address, delay, dispatcher) };
        if (_counted?.FacetKey != address.FacetKey)
        {
            _counted = address;
            work.Add(CountTraits(address, delay, dispatcher));
        }

        return Task.WhenAll(work);
    }

    [EffectMethod]
    public Task Handle(SearchRetried _, IDispatcher dispatcher)
    {
        if (state.Value.Address is not { } address)
        {
            return Task.CompletedTask;
        }

        return state.Value.Traits is RemoteData<TraitCounts>.Failed
            ? Task.WhenAll(Search(address, TimeSpan.Zero, dispatcher), CountTraits(address, TimeSpan.Zero, dispatcher))
            : Search(address, TimeSpan.Zero, dispatcher);
    }

    async Task Search(CategoryAddress address, TimeSpan delay, IDispatcher dispatcher)
    {
        var ct = Renew(ref _search);
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            dispatcher.Dispatch(new SearchStarted());
            var result = await api.SearchAsync(
                address.Category, address.Query, address.MinLevel, address.MaxLevel, address.Trait, address.Page, ct);
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

    async Task CountTraits(CategoryAddress address, TimeSpan delay, IDispatcher dispatcher)
    {
        var ct = Renew(ref _traits);
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            dispatcher.Dispatch(new TraitCountsStarted());
            var traits = await api.CountTraitsAsync(address.Category, address.Query, address.MinLevel, address.MaxLevel, ct);
            dispatcher.Dispatch(new TraitCountsLoaded(traits));
        }
        catch (OperationCanceledException)
        {
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new TraitCountsFailed(failure.Message));
        }
    }

    static CancellationToken Renew(ref CancellationTokenSource? pending)
    {
        Cancel(ref pending);
        pending = new CancellationTokenSource();
        return pending.Token;
    }

    static void Cancel(ref CancellationTokenSource? pending)
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }
}
