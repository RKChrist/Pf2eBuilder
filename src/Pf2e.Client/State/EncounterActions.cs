using Fluxor;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

// The encounter's half of the campaign screen. Every command here is refused by the server
// without a DM key, so the screen draws none of it for a player; the refusal is still the
// server's to make, and these actions carry no key of their own.

public sealed record CombatantPickerOpened;

public sealed record CombatantPickerClosed;

public sealed record MonsterQueryChanged(string Query);

public sealed record MonsterSearchStarted;

public sealed record MonsterSearchSucceeded(RuleSearchResult Result);

public sealed record MonsterSearchFailed(string Message);

/// <summary>A monster names a creature record and a player names somebody on the roster, so
/// exactly one of the two is set.</summary>
public sealed record CombatantAdded(string? RuleId, Guid? CharacterId, string? Name);

/// <summary>Everybody on the roster who is not in the fight yet, as one action rather than one
/// per character. Adding them as separate actions means separate commands racing each other on
/// the same campaign, which lost all but one of them.</summary>
public sealed record PartyAdded(IReadOnlyList<Guid> CharacterIds);

public sealed record InitiativeRolled;

public sealed record TurnAdvanced;

public sealed record ChangeUndone;

public sealed record MonsterRevealed(Guid CombatantId, bool Revealed);

/// <summary>What the GM has typed into one combatant's initiative box, before they commit it.
/// A draft rather than a send per keystroke, because "2" is on the way to "21".</summary>
public sealed record InitiativeDrafted(Guid CombatantId, string Draft);

public sealed record InitiativeSet(Guid CombatantId, int Initiative);

public sealed record CombatantRemoved(Guid CombatantId);

public sealed record EncounterEnded;

/// <summary>Null clears it, which is what "nothing in particular" means.</summary>
public sealed record ExplorationActivityChosen(Guid CharacterId, string? Activity);

public sealed record EditorOpened(Guid CharacterId);

public sealed record EditorClosed;

public sealed record EditDraftChanged(CharacterBuildEdit Draft);

public sealed record EditSubmitted;

public sealed record EditSucceeded;

public sealed record EditFailed(string Message);

public static class EncounterReducers
{
    [ReducerMethod]
    public static CampaignState On(CampaignState state, CombatantPickerOpened _) => state with
    {
        AddingCombatant = new CombatantPicker(string.Empty, new RemoteData<RuleSearchResult>.NotAsked()),
        ActionError = null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CombatantPickerClosed _) =>
        state with { AddingCombatant = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, MonsterQueryChanged action) => state with
    {
        AddingCombatant = state.AddingCombatant is { } picker ? picker with { Query = action.Query } : null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, MonsterSearchStarted _) => state with
    {
        AddingCombatant = state.AddingCombatant is { } picker
            ? picker with { Found = new RemoteData<RuleSearchResult>.Loading() }
            : null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, MonsterSearchSucceeded action) => state with
    {
        AddingCombatant = state.AddingCombatant is { } picker
            ? picker with { Found = new RemoteData<RuleSearchResult>.Loaded(action.Result) }
            : null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, MonsterSearchFailed action) => state with
    {
        AddingCombatant = state.AddingCombatant is { } picker
            ? picker with { Found = new RemoteData<RuleSearchResult>.Failed(action.Message) }
            : null,
    };

    /// <summary>The sheet stays open and says it is working, because adding four ogres is four
    /// taps on one screen and closing after each would make it twelve.</summary>
    [ReducerMethod]
    public static CampaignState On(CampaignState state, CombatantAdded _) => state with
    {
        AddingCombatant = state.AddingCombatant is { } picker ? picker with { Busy = true } : null,
        ActionError = null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, InitiativeDrafted action) => state with
    {
        InitiativeDrafts = new Dictionary<Guid, string>(state.InitiativeDrafts)
        {
            [action.CombatantId] = action.Draft,
        },
    };

    /// <summary>The box empties on the way out. What the server sends back is on the row, and a
    /// box still holding the number that produced it would be a second copy going stale.</summary>
    [ReducerMethod]
    public static CampaignState On(CampaignState state, InitiativeSet action) => state with
    {
        InitiativeDrafts = state.InitiativeDrafts
            .Where(entry => entry.Key != action.CombatantId)
            .ToDictionary(entry => entry.Key, entry => entry.Value),
        ActionError = null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CombatantRemoved _) =>
        state with { ActionError = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditorOpened action) => state with
    {
        Editing = state.Campaign is RemoteData<CampaignView>.Loaded loaded
                  && loaded.Value.Characters.FirstOrDefault(c => c.Id == action.CharacterId) is { } character
            ? new CharacterEditor(action.CharacterId, character.Build, false, null)
            : null,
        Breakdown = null,
        Picker = null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditorClosed _) => state with { Editing = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditDraftChanged action) => state with
    {
        Editing = state.Editing is { } editor ? editor with { Draft = action.Draft, Trouble = null } : null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditSubmitted _) => state with
    {
        Editing = state.Editing is { } editor ? editor with { Saving = true, Trouble = null } : null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditSucceeded _) => state with { Editing = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EditFailed action) => state with
    {
        Editing = state.Editing is { } editor ? editor with { Saving = false, Trouble = action.Message } : null,
    };
}
