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

public sealed record OpenBreakdown(Guid CharacterId, StatSlot Stat);

/// <summary>The rules arm's search lives here rather than beside the browse screen's, because
/// looking for a buff must not disturb whatever the player had filtered on the other screen,
/// and because the search is over the moment the sheet closes.</summary>
public sealed record EffectPicker(
    Guid CharacterId,
    PickerArm Arm,
    string Query,
    RemoteData<RuleSearchResult> Found);

public sealed record EffectDraft(string Name, bool Bonus, int Value, string Type, string Applies)
{
    public static EffectDraft Fresh { get; } = new(string.Empty, true, 1, "Status", "stat:ArmorClass");

    public int Signed => Bonus ? Value : -Value;
}

[FeatureState]
public sealed record PartyState
{
    public string Code { get; init; } = string.Empty;

    public string CodeDraft { get; init; } = string.Empty;

    public RemoteData<TableView> Table { get; init; } = new RemoteData<TableView>.NotAsked();

    public string PasteDraft { get; init; } = string.Empty;

    /// <summary>Separate from <see cref="Table"/> on purpose: a paste that will not import must
    /// not blank the table everyone at it is reading.</summary>
    public bool Importing { get; init; }

    public string? ImportError { get; init; }

    public string? ActionError { get; init; }

    public bool Live { get; init; }

    public OpenBreakdown? Breakdown { get; init; }

    public EffectPicker? Picker { get; init; }

    public EffectDraft Draft { get; init; } = EffectDraft.Fresh;
}

public sealed record CodeDraftChanged(string Draft);

public sealed record JoinRequested(string Code);

public sealed record TableCreationRequested;

public sealed record TableOpened(TableView Table);

public sealed record TableFailed(string Message);

public sealed record PasteDraftChanged(string Draft);

public sealed record ImportRequested;

public sealed record ImportSucceeded;

public sealed record ImportFailed(string Message);

/// <summary>The one way a changed character reaches the store. A command's answer and the hub's
/// push both arrive here, so a local change and somebody else's are the same render.</summary>
public sealed record CharacterUpdated(CharacterSheetView Character);

public sealed record HitPointsNudged(Guid CharacterId, int Delta);

public sealed record EffectSet(Guid CharacterId, Guid Slot, EffectSpec? Effect);

/// <summary>The draft travels with the action because its reducer empties the form, and a
/// reducer runs before the effect that has to send what was in it.</summary>
public sealed record CustomEffectAdded(Guid CharacterId, EffectDraft Draft);

public sealed record RuleEffectRequested(Guid CharacterId, string RuleId, string Name);

public sealed record ActionFailed(string Message);

public sealed record LiveJoined;

public sealed record LiveLost;

public sealed record BreakdownOpened(Guid CharacterId, StatSlot Stat);

public sealed record BreakdownClosed;

public sealed record PickerOpened(Guid CharacterId);

public sealed record PickerClosed;

public sealed record ArmSelected(PickerArm Arm);

public sealed record RuleQueryChanged(string Query);

public sealed record RuleSearchStarted;

public sealed record RuleSearchSucceeded(RuleSearchResult Result);

public sealed record RuleSearchFailed(string Message);

public sealed record RuleSearchCleared;

/// <summary>A record that states no modifier this app can apply is not a dead end: it becomes a
/// custom effect already carrying its name.</summary>
public sealed record RuleDeclined(string Name);

public sealed record DraftChanged(EffectDraft Draft);

public static class PartyReducers
{
    [ReducerMethod]
    public static PartyState On(PartyState state, CodeDraftChanged action) =>
        state with { CodeDraft = action.Draft };

    [ReducerMethod]
    public static PartyState On(PartyState state, JoinRequested _) =>
        state with { Table = new RemoteData<TableView>.Loading() };

    [ReducerMethod]
    public static PartyState On(PartyState state, TableCreationRequested _) =>
        state with { Table = new RemoteData<TableView>.Loading() };

    [ReducerMethod]
    public static PartyState On(PartyState state, TableOpened action) => state with
    {
        Code = action.Table.Code,
        CodeDraft = string.Empty,
        Table = new RemoteData<TableView>.Loaded(action.Table),
        ImportError = null,
        ActionError = null,
    };

    [ReducerMethod]
    public static PartyState On(PartyState state, TableFailed action) =>
        state with { Table = new RemoteData<TableView>.Failed(action.Message) };

    [ReducerMethod]
    public static PartyState On(PartyState state, PasteDraftChanged action) =>
        state with { PasteDraft = action.Draft };

    [ReducerMethod]
    public static PartyState On(PartyState state, ImportRequested _) =>
        state with { Importing = true, ImportError = null };

    [ReducerMethod]
    public static PartyState On(PartyState state, ImportSucceeded _) =>
        state with { Importing = false, ImportError = null, PasteDraft = string.Empty };

    [ReducerMethod]
    public static PartyState On(PartyState state, ImportFailed action) =>
        state with { Importing = false, ImportError = action.Message };

    [ReducerMethod]
    public static PartyState On(PartyState state, CharacterUpdated action)
    {
        if (state.Table is not RemoteData<TableView>.Loaded loaded)
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
            Table = new RemoteData<TableView>.Loaded(
                loaded.Value with { Exists = true, Characters = replaced }),
        };
    }

    [ReducerMethod]
    public static PartyState On(PartyState state, ActionFailed action) =>
        state with { ActionError = action.Message };

    [ReducerMethod]
    public static PartyState On(PartyState state, LiveJoined _) =>
        state with { Live = true };

    [ReducerMethod]
    public static PartyState On(PartyState state, LiveLost _) =>
        state with { Live = false };

    [ReducerMethod]
    public static PartyState On(PartyState state, BreakdownOpened action) =>
        state with { Breakdown = new OpenBreakdown(action.CharacterId, action.Stat), Picker = null };

    [ReducerMethod]
    public static PartyState On(PartyState state, BreakdownClosed _) =>
        state with { Breakdown = null };

    [ReducerMethod]
    public static PartyState On(PartyState state, PickerOpened action) => state with
    {
        Picker = new EffectPicker(
            action.CharacterId,
            PickerArm.Conditions,
            string.Empty,
            new RemoteData<RuleSearchResult>.NotAsked()),
        Breakdown = null,
        Draft = EffectDraft.Fresh,
    };

    [ReducerMethod]
    public static PartyState On(PartyState state, PickerClosed _) =>
        state with { Picker = null };

    [ReducerMethod]
    public static PartyState On(PartyState state, ArmSelected action) =>
        state.Picker is { } picker ? state with { Picker = picker with { Arm = action.Arm } } : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleQueryChanged action) =>
        state.Picker is { } picker ? state with { Picker = picker with { Query = action.Query } } : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleSearchStarted _) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Loading() } }
            : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleSearchSucceeded action) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Loaded(action.Result) } }
            : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleSearchFailed action) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.Failed(action.Message) } }
            : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleSearchCleared _) =>
        state.Picker is { } picker
            ? state with { Picker = picker with { Found = new RemoteData<RuleSearchResult>.NotAsked() } }
            : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, RuleDeclined action) =>
        state.Picker is { } picker
            ? state with
            {
                Picker = picker with { Arm = PickerArm.Custom },
                Draft = state.Draft with { Name = action.Name },
            }
            : state;

    [ReducerMethod]
    public static PartyState On(PartyState state, DraftChanged action) =>
        state with { Draft = action.Draft };

    [ReducerMethod]
    public static PartyState On(PartyState state, CustomEffectAdded _) =>
        state with { Draft = EffectDraft.Fresh };
}
