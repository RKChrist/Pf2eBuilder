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
    readonly CampaignHub _hub;
    readonly IState<PartyState> _state;
    readonly TimeSpan _debounce;

    /// <summary>Cancelling the previous search is what makes the last keystroke win.</summary>
    CancellationTokenSource? _pending;

    public PartyEffects(
        TrackerApi tracker,
        RulesApi rules,
        CampaignHub hub,
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
        hub.ModeChanged += mode => dispatcher.Dispatch(new ModeChanged(mode));
    }

    [EffectMethod]
    public async Task Handle(JoinRequested action, IDispatcher dispatcher)
    {
        var code = CampaignCodes.Normalize(action.Code);
        if (!CampaignCodes.IsValid(code))
        {
            dispatcher.Dispatch(new CampaignFailed("A campaign code is four to twelve letters and digits."));
            return;
        }

        await OpenAsync(code, dispatcher);
    }

    [EffectMethod]
    public async Task Handle(CampaignCreationRequested _, IDispatcher dispatcher)
    {
        try
        {
            var created = await _tracker.CreateCampaignAsync(CancellationToken.None);
            dispatcher.Dispatch(new CampaignCreated(created));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new CampaignFailed(failure.Message));
        }
    }

    /// <summary>The DM key arrives once, with the campaign that was just created, and is held
    /// for the rest of the session. Reloading the page makes this browser a player, which is the
    /// honest consequence of a secret nobody wrote down.</summary>
    [EffectMethod]
    public async Task Handle(CampaignCreated action, IDispatcher dispatcher)
    {
        _tracker.UseDmKey(action.Created.DmKey);
        await OpenAsync(action.Created.Code, dispatcher);
    }

    /// <summary>
    /// Catching everything is deliberate: a hub that cannot connect throws several unrelated
    /// types, and any of them left unobserved is a console error on a screen that is otherwise
    /// working. Not live is a state a player can be shown.
    /// </summary>
    [EffectMethod]
    public async Task Handle(CampaignOpened action, IDispatcher dispatcher)
    {
        try
        {
            await _hub.JoinAsync(action.Campaign.Code, _state.Value.DmKey, CancellationToken.None);
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
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ImportFailed(failure.Message));
        }
    }

    [EffectMethod]
    public async Task Handle(ModeChangeRequested action, IDispatcher dispatcher)
    {
        try
        {
            // The answer is dispatched as well as pushed, so the DM's own screen moves on the
            // tap rather than on the round trip back through the hub.
            dispatcher.Dispatch(new ModeChanged(
                await _tracker.SetModeAsync(_state.Value.Code, action.Mode, CancellationToken.None)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
        }
    }

    [EffectMethod]
    public Task Handle(HitPointsNudged action, IDispatcher dispatcher) =>
        ApplyAsync(dispatcher, code =>
            _tracker.ChangeHitPointsAsync(code, action.CharacterId, action.Delta, CancellationToken.None));

    [EffectMethod]
    public Task Handle(EffectSet action, IDispatcher dispatcher) =>
        ApplyToCampaignAsync(dispatcher, code => _tracker.ApplyEffectAsync(
            code, action.Slot, action.Effect,
            [new EffectTargetSpec("Character", action.CharacterId)],
            CancellationToken.None));

    [EffectMethod]
    public Task Handle(CustomEffectAdded action, IDispatcher dispatcher)
    {
        var draft = action.Draft;
        var modifier = new EffectModifierView(
            draft.Type, draft.Signed, [EffectVocabulary.Of(draft.Applies).Spec]);
        var effect = new EffectSpec(draft.Name.Trim(), "Custom", null, 0, null, [modifier]);

        return ApplyToCampaignAsync(dispatcher, code => _tracker.ApplyEffectAsync(
            code, Guid.NewGuid(), effect,
            [new EffectTargetSpec("Character", action.CharacterId)],
            CancellationToken.None));
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
        await ApplyToCampaignAsync(dispatcher, code => _tracker.ApplyEffectAsync(
            code, Guid.NewGuid(), effect,
            [new EffectTargetSpec("Character", action.CharacterId)],
            CancellationToken.None));
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
            dispatcher.Dispatch(new CampaignOpened(await _tracker.GetCampaignAsync(code, CancellationToken.None)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new CampaignFailed(failure.Message));
        }
    }

    async Task ApplyAsync(IDispatcher dispatcher, Func<string, Task<CharacterSheetView>> call)
    {
        try
        {
            dispatcher.Dispatch(new CharacterUpdated(await call(_state.Value.Code)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
        }
    }

    /// <summary>One effect can reach the whole party, so the answer is the whole campaign rather
    /// than one sheet. It refreshes rather than reopening, because reopening would rejoin the
    /// hub and this connection is already in the right groups.</summary>
    async Task ApplyToCampaignAsync(IDispatcher dispatcher, Func<string, Task<CampaignView>> call)
    {
        try
        {
            dispatcher.Dispatch(new CampaignRefreshed(await call(_state.Value.Code)));
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new ActionFailed(failure.Message));
        }
    }
}
