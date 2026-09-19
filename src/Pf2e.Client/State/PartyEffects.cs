using Fluxor;
using Microsoft.Extensions.Options;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

public sealed class PartyEffects
{
    readonly TrackerApi _tracker;
    readonly RulesApi _rules;
    readonly TableHub _hub;
    readonly IState<PartyState> _state;
    readonly TimeSpan _debounce;

    /// <summary>Cancelling the previous search is what makes the last keystroke win.</summary>
    CancellationTokenSource? _pending;

    public PartyEffects(
        TrackerApi tracker,
        RulesApi rules,
        TableHub hub,
        IState<PartyState> state,
        IOptions<ApiOptions> options,
        IDispatcher dispatcher)
    {
        _tracker = tracker;
        _rules = rules;
        _hub = hub;
        _state = state;
        _debounce = TimeSpan.FromMilliseconds(options.Value.SearchDebounceMilliseconds);

        hub.CharacterChanged += sheet => dispatcher.Dispatch(new CharacterUpdated(sheet));
    }

    [EffectMethod]
    public async Task Handle(JoinRequested action, IDispatcher dispatcher)
    {
        var code = TableCodes.Normalize(action.Code);
        if (!TableCodes.IsValid(code))
        {
            dispatcher.Dispatch(new TableFailed("A table code is four to twelve letters and digits."));
            return;
        }

        await OpenAsync(code, dispatcher);
    }

    [EffectMethod]
    public async Task Handle(TableCreationRequested _, IDispatcher dispatcher)
    {
        const int draws = 5;

        for (var draw = 0; draw < draws; draw++)
        {
            TableView table;
            try
            {
                table = await _tracker.GetTableAsync(TableCodes.Draw(), CancellationToken.None);
            }
            catch (TrackerApiException failure)
            {
                dispatcher.Dispatch(new TableFailed(failure.Message));
                return;
            }

            if (!table.Exists)
            {
                dispatcher.Dispatch(new TableOpened(table));
                return;
            }
        }

        dispatcher.Dispatch(new TableFailed(
            $"Every one of {draws} codes this app drew is already a table. Try again."));
    }

    /// <summary>
    /// Catching everything is deliberate: a hub that cannot connect throws several unrelated
    /// types, and any of them left unobserved is a console error on a screen that is otherwise
    /// working. Not live is a state a player can be shown.
    /// </summary>
    [EffectMethod]
    public async Task Handle(TableOpened action, IDispatcher dispatcher)
    {
        try
        {
            await _hub.JoinAsync(action.Table.Code, CancellationToken.None);
            dispatcher.Dispatch(new LiveJoined());
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            dispatcher.Dispatch(new LiveLost());
        }
    }

    [EffectMethod]
    public async Task Handle(ImportRequested _, IDispatcher dispatcher)
    {
        var now = _state.Value;

        try
        {
            dispatcher.Dispatch(new CharacterUpdated(
                await _tracker.ImportAsync(now.Code, now.PasteDraft, CancellationToken.None)));
            dispatcher.Dispatch(new ImportSucceeded());
        }
        catch (TrackerApiException failure)
        {
            dispatcher.Dispatch(new ImportFailed(failure.Message));
        }
    }

    [EffectMethod]
    public Task Handle(HitPointsNudged action, IDispatcher dispatcher) =>
        ApplyAsync(dispatcher, code =>
            _tracker.ChangeHitPointsAsync(code, action.CharacterId, action.Delta, CancellationToken.None));

    [EffectMethod]
    public Task Handle(EffectSet action, IDispatcher dispatcher) =>
        ApplyAsync(dispatcher, code =>
            _tracker.SetEffectAsync(code, action.CharacterId, action.Slot, action.Effect, CancellationToken.None));

    [EffectMethod]
    public Task Handle(CustomEffectAdded action, IDispatcher dispatcher)
    {
        var draft = action.Draft;
        var modifier = new EffectModifierView(
            draft.Type, draft.Signed, [EffectVocabulary.Of(draft.Applies).Spec]);
        var effect = new EffectSpec(draft.Name.Trim(), "Custom", null, 0, null, [modifier]);

        return ApplyAsync(dispatcher, code =>
            _tracker.SetEffectAsync(code, action.CharacterId, Guid.NewGuid(), effect, CancellationToken.None));
    }

    [EffectMethod]
    public async Task Handle(RuleEffectRequested action, IDispatcher dispatcher)
    {
        RuleDetail? rule;
        try
        {
            rule = await _rules.GetRuleAsync(action.RuleId, CancellationToken.None);
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
            return;
        }

        // A record the catalogue no longer holds reads as a missing record, not as a failure to
        // reach the server, because those are two different things to a player mid-combat.
        if (rule is null)
        {
            dispatcher.Dispatch(new ActionFailed($"{action.Name} is no longer in the rules."));
            return;
        }

        var effect = new EffectSpec(action.Name, "Rule", action.RuleId, 0, null, rule.Modifiers);
        await ApplyAsync(dispatcher, code =>
            _tracker.SetEffectAsync(code, action.CharacterId, Guid.NewGuid(), effect, CancellationToken.None));
    }

    [EffectMethod]
    public async Task Handle(RuleQueryChanged action, IDispatcher dispatcher)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;

        if (string.IsNullOrWhiteSpace(action.Query))
        {
            dispatcher.Dispatch(new RuleSearchCleared());
            return;
        }

        var mine = new CancellationTokenSource();
        _pending = mine;

        try
        {
            await Task.Delay(_debounce, mine.Token);
            dispatcher.Dispatch(new RuleSearchStarted());
            dispatcher.Dispatch(new RuleSearchSucceeded(
                await _rules.SearchAsync(null, action.Query, null, null, null, 1, mine.Token)));
        }
        catch (OperationCanceledException)
        {
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new RuleSearchFailed(failure.Message));
        }
    }

    async Task OpenAsync(string code, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new TableOpened(await _tracker.GetTableAsync(code, CancellationToken.None)));
        }
        catch (TrackerApiException failure)
        {
            dispatcher.Dispatch(new TableFailed(failure.Message));
        }
    }

    async Task ApplyAsync(IDispatcher dispatcher, Func<string, Task<CharacterSheetView>> call)
    {
        try
        {
            dispatcher.Dispatch(new CharacterUpdated(await call(_state.Value.Code)));
        }
        catch (TrackerApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
        }
    }
}
