using Fluxor;
using Microsoft.Extensions.Options;
using Pf2e.Client.Api;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

/// <summary>
/// The encounter's commands and the character editor. Separate from <see cref="CampaignEffects"/>
/// because they are the DM's half of the screen and because that file was already the longest in
/// the client.
/// <para>Nothing here checks a role. Every one of these is refused by the server without a DM
/// key, and a second opinion in the browser would be a second place to get it wrong.</para>
/// </summary>
public sealed class EncounterEffects
{
    readonly TrackerApi _tracker;
    readonly RulesApi _rules;
    readonly IState<CampaignState> _state;
    readonly TimeSpan _debounce;

    CancellationTokenSource? _pending;

    public EncounterEffects(
        TrackerApi tracker, RulesApi rules, IState<CampaignState> state, IOptions<ApiOptions> options)
    {
        _tracker = tracker;
        _rules = rules;
        _state = state;
        _debounce = TimeSpan.FromMilliseconds(options.Value.SearchDebounceMilliseconds);
    }

    [EffectMethod]
    public Task Handle(CombatantAdded action, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.AddCombatantAsync(
            code, action.RuleId, action.CharacterId, action.Name, null, CancellationToken.None));

    [EffectMethod]
    public Task Handle(InitiativeRolled _, IDispatcher dispatcher) =>
        // An empty list rolls for everyone the DM has not typed a number for, which is everyone.
        RunAsync(dispatcher, code => _tracker.RollInitiativeAsync(code, [], CancellationToken.None));

    [EffectMethod]
    public Task Handle(TurnAdvanced _, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.NextTurnAsync(code, CancellationToken.None));

    [EffectMethod]
    public Task Handle(ChangeUndone _, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.UndoAsync(code, CancellationToken.None));

    [EffectMethod]
    public Task Handle(MonsterRevealed action, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.RevealAsync(
            code, action.CombatantId, action.Revealed, CancellationToken.None));

    [EffectMethod]
    public Task Handle(CampActivityTaken action, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.TakeCampActivityAsync(
            code, action.CharacterId, action.Activity, CancellationToken.None));

    [EffectMethod]
    public Task Handle(NightRested _, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.RestAsync(code, CancellationToken.None));

    [EffectMethod]
    public Task Handle(ExplorationActivityChosen action, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.SetExplorationActivityAsync(
            code, action.CharacterId, action.Activity, CancellationToken.None));

    [EffectMethod]
    public Task Handle(EncounterEnded _, IDispatcher dispatcher) =>
        RunAsync(dispatcher, code => _tracker.EndEncounterAsync(code, CancellationToken.None));

    [EffectMethod]
    public async Task Handle(EditSubmitted _, IDispatcher dispatcher)
    {
        if (_state.Value.Editing is not { } editor)
        {
            return;
        }

        try
        {
            var character = await _tracker.EditCharacterAsync(
                _state.Value.Code, editor.CharacterId, editor.Draft, CancellationToken.None);

            // The changed character is dispatched as well as pushed, so the editor closes on the
            // save rather than on the round trip back through the hub.
            dispatcher.Dispatch(new CharacterUpdated(character));
            dispatcher.Dispatch(new EditSucceeded());
        }
        catch (CampaignApiException failure)
        {
            dispatcher.Dispatch(new EditFailed(failure.Message));
        }
    }

    /// <summary>
    /// The monster search, debounced the same way the effect picker's is. Scoped to creatures,
    /// because the DM is choosing something to fight and the other 21,809 records are not that.
    /// </summary>
    [EffectMethod]
    public async Task Handle(MonsterQueryChanged action, IDispatcher dispatcher)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;

        var typed = action.Query.Trim();
        if (typed.Length == 0)
        {
            return;
        }

        var token = new CancellationTokenSource();
        _pending = token;

        try
        {
            await Task.Delay(_debounce, token.Token);
            dispatcher.Dispatch(new MonsterSearchStarted());
            dispatcher.Dispatch(new MonsterSearchSucceeded(
                await _rules.SearchAsync(
                    "creature", typed, null, null, null, 1, token.Token, pageSize: 20)));
        }
        catch (OperationCanceledException)
        {
        }
        catch (RulesApiException failure)
        {
            dispatcher.Dispatch(new MonsterSearchFailed(failure.Message));
        }
    }

    async Task RunAsync(IDispatcher dispatcher, Func<string, Task<CampaignView>> call)
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
