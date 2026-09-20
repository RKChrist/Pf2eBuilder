using Fluxor;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

public enum PickerArm
{
    Conditions,
    Rules,
    Custom,
}

public sealed record OpenBreakdown(Guid CharacterId, StatAddress Stat);

/// <summary>The rules arm's search lives here rather than beside the browse screen's, because
/// looking for a buff must not disturb whatever the player had filtered on the other screen,
/// and because the search is over the moment the sheet closes.</summary>
/// <summary>Who an effect is being put on: the kind the server resolves it against and the id
/// it resolves. Two kinds rather than one widened id, because the server checks the stated kind
/// against what is actually there and answers a monster named as a character with a refusal.</summary>
public sealed record EffectSubject(string Kind, Guid Id)
{
    public static EffectSubject Character(Guid id) => new("Character", id);

    public static EffectSubject Monster(Guid id) => new("Monster", id);
}

public sealed record EffectPicker(
    EffectSubject Subject,
    PickerArm Arm,
    string Query,
    RemoteData<RuleSearchResult> Found,
    /// <summary>What is typed into the effects filter. Separate from <paramref name="Query"/>,
    /// which searches the server on every keystroke: this one only hides rows already on
    /// screen.</summary>
    string Filter = "");

public sealed record EffectDraft(string Name, bool Bonus, int Value, string Type, string Applies)
{
    public static EffectDraft Fresh { get; } = new(string.Empty, true, 1, "Status", "stat:ArmorClass");

    public int Signed => Bonus ? Value : -Value;
}

[FeatureState]
public sealed record CampaignState
{
    public string Code { get; init; } = string.Empty;

    public string CodeDraft { get; init; } = string.Empty;

    /// <summary>Held for this browser session only. It arrives once, with the campaign this
    /// browser created, and a reload makes this browser a player again.</summary>
    public string? DmKey { get; init; }

    public RemoteData<CampaignView> Campaign { get; init; } = new RemoteData<CampaignView>.NotAsked();

    public string PasteDraft { get; init; } = string.Empty;

    /// <summary>A chosen file, held whole rather than poured into the paste box. A Wanderer's
    /// Guide export is twelve megabytes because it embeds every item and spell record it
    /// mentions, and a textarea bound to that re-renders the whole party on every keystroke
    /// anywhere on the page.</summary>
    public ChosenFile? File { get; init; }

    /// <summary>What an import would send: the file when one is chosen, and otherwise whatever
    /// was pasted. One reading, so the button and the request cannot disagree about which.</summary>
    public string Offered => File?.Json ?? PasteDraft;

    /// <summary>Separate from <see cref="Campaign"/> on purpose: a paste that will not import must
    /// not blank the campaign everyone in it is reading.</summary>
    public bool Importing { get; init; }

    public string? ImportError { get; init; }

    public string? ActionError { get; init; }

    public bool Live { get; init; }

    public OpenBreakdown? Breakdown { get; init; }

    public EffectPicker? Picker { get; init; }

    public EffectDraft Draft { get; init; } = EffectDraft.Fresh;

    /// <summary>What each card's hit point field holds, keyed by character, because two cards
    /// are open at once and one shared number would send the fighter's damage to the bard.</summary>
    public IReadOnlyDictionary<Guid, string> HitPointDrafts { get; init; } =
        new Dictionary<Guid, string>();

    public string HitPointDraft(Guid characterId) =>
        HitPointDrafts.TryGetValue(characterId, out var typed) ? typed : string.Empty;

    /// <summary>What each initiative box holds while it is being typed, keyed by combatant.
    /// Separate from the initiative on the campaign, which is what the server last confirmed:
    /// a box showing "1" on the way to "12" must not be mistaken for a committed number.</summary>
    public IReadOnlyDictionary<Guid, string> InitiativeDrafts { get; init; } =
        new Dictionary<Guid, string>();

    public string InitiativeDraft(Guid combatantId) =>
        InitiativeDrafts.TryGetValue(combatantId, out var typed) ? typed : string.Empty;

    /// <summary>Open while the DM is choosing somebody to put in the fight. Null the rest of
    /// the time, which is what keeps the sheet shut.</summary>
    public CombatantPicker? AddingCombatant { get; init; }

    /// <summary>The character being edited and the values as typed. A draft rather than the
    /// loaded character, because a half-typed level must not be sent and must not be lost when
    /// somebody else's change arrives.</summary>
    public CharacterEditor? Editing { get; init; }
}

/// <summary>Adding to the fight is two searches over one screen: the roster, which is short and
/// needs no search, and the 3,786 seeded creatures, which needs one.</summary>
public sealed record CombatantPicker(
    string Query,
    RemoteData<RuleSearchResult> Found,
    bool Busy = false);

public sealed record CharacterEditor(Guid CharacterId, CharacterBuildEdit Draft, bool Saving, string? Trouble);

/// <summary>A file as it was read, with the name to show for it. Bytes rather than characters
/// for the size, because that is the number the upload limit is written in.</summary>
public sealed record ChosenFile(string Name, long Bytes, string Json);

public sealed record CodeDraftChanged(string Draft);

public sealed record JoinRequested(string Code);

public sealed record CampaignCreationRequested;

public sealed record CampaignOpened(CampaignView Campaign);

public sealed record CampaignCreated(CreatedCampaignView Created);

public sealed record CampaignFailed(string Message);

/// <summary>A command whose answer is the whole campaign. It replaces the loaded value without
/// reopening, because reopening would rejoin a hub connection that is already in the right
/// groups.</summary>
public sealed record CampaignRefreshed(CampaignView Campaign);

public sealed record ModeChangeRequested(string Mode);

public sealed record ModeChanged(CampaignModeView Mode);

public sealed record PasteDraftChanged(string Draft);

/// <summary>A file was read off the disk. Wanderer's Guide only offers a download, so pasting
/// its export means finding the file, opening it in something that can show twelve megabytes of
/// JSON, and selecting all of it.</summary>
public sealed record FileChosen(ChosenFile File);

public sealed record FileRejected(string Message);

public sealed record FileCleared;

public sealed record ImportRequested;

public sealed record ImportSucceeded;

public sealed record ImportFailed(string Message);

/// <summary>The one way a changed character reaches the store. A command's answer and the hub's
/// push both arrive here, so a local change and somebody else's are the same render.</summary>
public sealed record CharacterUpdated(CharacterSheetView Character);

public sealed record HitPointDraftChanged(Guid CharacterId, string Draft);

/// <summary>
/// The typed amount, which is the primary control: losing a hundred hit points is one number and
/// one round trip rather than a hundred taps. Direction is "Damage" or "Heal".
/// <para>The amount travels with the action because its reducer empties the field, and a reducer
/// runs before the effect that has to send what was in it.</para>
/// </summary>
public sealed record HitPointsApplied(Guid CharacterId, int Amount, string Direction);

public sealed record EffectSet(EffectSubject Subject, Guid Slot, EffectSpec? Effect);

/// <summary>The draft travels with the action because its reducer empties the form, and a
/// reducer runs before the effect that has to send what was in it.</summary>
public sealed record CustomEffectAdded(EffectSubject Subject, EffectDraft Draft);

public sealed record RuleEffectRequested(EffectSubject Subject, string RuleId, string Name);

public sealed record ActionFailed(string Message);

public sealed record LiveJoined;

public sealed record LiveLost;

public sealed record BreakdownOpened(Guid CharacterId, StatAddress Stat);

public sealed record BreakdownClosed;

public sealed record PickerOpened(EffectSubject Subject);

public sealed record PickerClosed;

public sealed record ArmSelected(PickerArm Arm);

public sealed record EffectFilterChanged(string Filter);

public sealed record RuleQueryChanged(string Query);

public sealed record RuleSearchStarted;

public sealed record RuleSearchSucceeded(RuleSearchResult Result);

public sealed record RuleSearchFailed(string Message);

public sealed record RuleSearchCleared;

/// <summary>A record that states no modifier this app can apply is not a dead end: it becomes a
/// custom effect already carrying its name.</summary>
public sealed record RuleDeclined(string Name);

public sealed record DraftChanged(EffectDraft Draft);

public static class CampaignReducers
{
    [ReducerMethod]
    public static CampaignState On(CampaignState state, CodeDraftChanged action) =>
        state with { CodeDraft = action.Draft };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, JoinRequested _) =>
        state with { Campaign = new RemoteData<CampaignView>.Loading() };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CampaignCreationRequested _) =>
        state with { Campaign = new RemoteData<CampaignView>.Loading() };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CampaignOpened action) => state with
    {
        Code = action.Campaign.Code,
        CodeDraft = string.Empty,
        Campaign = new RemoteData<CampaignView>.Loaded(action.Campaign),
        ImportError = null,
        ActionError = null,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CampaignFailed action) =>
        state with { Campaign = new RemoteData<CampaignView>.Failed(action.Message) };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CampaignRefreshed action) =>
        state with { Campaign = new RemoteData<CampaignView>.Loaded(action.Campaign) };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CampaignCreated action) =>
        state with { DmKey = action.Created.DmKey };

    // Each of the two ways in clears the other, because two half filled ways to say the same
    // thing is a screen that cannot say which one it will send. The first version claimed this
    // and only did half of it, so a chosen file silently beat a paste with both on screen.
    [ReducerMethod]
    public static CampaignState On(CampaignState state, PasteDraftChanged action) =>
        state with
        {
            PasteDraft = action.Draft,
            File = action.Draft.Length > 0 ? null : state.File,
        };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, FileChosen action) =>
        state with { File = action.File, PasteDraft = string.Empty, ImportError = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, FileRejected action) =>
        state with { File = null, ImportError = action.Message };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, FileCleared _) =>
        state with { File = null, ImportError = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, ImportRequested _) =>
        state with { Importing = true, ImportError = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, ImportSucceeded _) =>
        state with { Importing = false, ImportError = null, PasteDraft = string.Empty, File = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, ImportFailed action) =>
        state with { Importing = false, ImportError = action.Message };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CharacterUpdated action)
    {
        if (state.Campaign is not RemoteData<CampaignView>.Loaded loaded)
        {
            return state;
        }

        var characters = loaded.Value.Characters;
        IReadOnlyList<CharacterSheetView> replaced =
            characters.Any(character => character.Id == action.Character.Id)
                ? [.. characters.Select(character =>
                    character.Id == action.Character.Id ? action.Character : character)]
                : [.. characters, action.Character];

        return state with
        {
            Campaign = new RemoteData<CampaignView>.Loaded(
                loaded.Value with { Characters = replaced }),
        };
    }

    /// <summary>The DM's own tap and somebody else's push land here as the same value, which is
    /// what makes "every screen follows" one code path rather than two.</summary>
    [ReducerMethod]
    public static CampaignState On(CampaignState state, ModeChanged action) =>
        state.Campaign is RemoteData<CampaignView>.Loaded loaded
            ? state with
            {
                Campaign = new RemoteData<CampaignView>.Loaded(
                    loaded.Value with { Mode = action.Mode.Mode }),
            }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, HitPointDraftChanged action) =>
        state with { HitPointDrafts = Drafts(state, action.CharacterId, action.Draft) };

    /// <summary>The field empties on the tap, so a DM who taps Damage twice by accident does not
    /// apply the number twice.</summary>
    [ReducerMethod]
    public static CampaignState On(CampaignState state, HitPointsApplied action) =>
        state with { HitPointDrafts = Drafts(state, action.CharacterId, string.Empty) };

    static Dictionary<Guid, string> Drafts(CampaignState state, Guid characterId, string draft) =>
        new(state.HitPointDrafts) { [characterId] = draft };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, ActionFailed action) =>
        state with { ActionError = action.Message };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, LiveJoined _) =>
        state with { Live = true };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, LiveLost _) =>
        state with { Live = false };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, BreakdownOpened action) =>
        state with { Breakdown = new OpenBreakdown(action.CharacterId, action.Stat), Picker = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, BreakdownClosed _) =>
        state with { Breakdown = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, PickerOpened action) => state with
    {
        Picker = new EffectPicker(
            action.Subject,
            PickerArm.Conditions,
            string.Empty,
            new RemoteData<RuleSearchResult>.NotAsked()),
        Breakdown = null,
        Draft = EffectDraft.Fresh,
    };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, PickerClosed _) =>
        state with { Picker = null };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, ArmSelected action) =>
        state.Picker is { } picker ? state with { Picker = picker with { Arm = action.Arm } } : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, EffectFilterChanged action) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Filter = action.Filter } }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleQueryChanged action) =>
        state.Picker is { } picker ? state with { Picker = picker with { Query = action.Query } } : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleSearchStarted _) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Loading() } }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleSearchSucceeded action) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Loaded(action.Result) } }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleSearchFailed action) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Failed(action.Message) } }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleSearchCleared _) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.NotAsked() } }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, RuleDeclined action) =>
        state.Picker is { } picker
            ? state with
            {
                Picker = picker with { Arm = PickerArm.Custom },
                Draft = state.Draft with { Name = action.Name },
            }
            : state;

    [ReducerMethod]
    public static CampaignState On(CampaignState state, DraftChanged action) =>
        state with { Draft = action.Draft };

    [ReducerMethod]
    public static CampaignState On(CampaignState state, CustomEffectAdded _) =>
        state with { Draft = EffectDraft.Fresh };
}
