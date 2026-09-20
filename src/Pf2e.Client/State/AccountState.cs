using Fluxor;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Client.State;

/// <summary>
/// Who this browser is, as far as the server is concerned.
/// <para>Four states rather than <see cref="RemoteData{T}"/> over a nullable account, because
/// "nobody is signed in" is an answer and not a failure. Modelled as a union so a screen cannot
/// accidentally read a loaded-but-empty session as a signed-in one.</para>
/// </summary>
public abstract record Session
{
    /// <summary>Nobody has asked the server yet.</summary>
    public sealed record Unknown : Session;

    public sealed record Asking : Session;

    public sealed record In(AccountView Account) : Session;

    /// <summary>Asked, and nobody is signed in here.</summary>
    public sealed record Out : Session;
}

public enum AccountForm
{
    SignIn,
    Register,
}

public sealed record AccountDraft(string Email, string DisplayName, string Password)
{
    public static AccountDraft Empty { get; } = new(string.Empty, string.Empty, string.Empty);
}

[FeatureState]
public sealed record AccountState
{
    public Session Session { get; init; } = new Session.Unknown();

    public AccountForm Form { get; init; } = AccountForm.SignIn;

    public AccountDraft Draft { get; init; } = AccountDraft.Empty;

    /// <summary>True while a submission is in flight, so the button can say so and cannot be
    /// pressed twice into two accounts.</summary>
    public bool Working { get; init; }

    /// <summary>What the server refused, in its own words.</summary>
    public string? Trouble { get; init; }
}

/// <summary>Asked once, on the first paint of anything that shows who you are.</summary>
public sealed record SessionAsked;

public sealed record SessionAnswered(AccountView? Account);

public sealed record AccountDraftChanged(AccountDraft Draft);

public sealed record AccountFormSwitched(AccountForm Form);

public sealed record SignInSubmitted;

public sealed record RegistrationSubmitted;

public sealed record SignOutRequested;

public sealed record AccountRefused(string Message);

public static class AccountReducers
{
    [ReducerMethod]
    public static AccountState On(AccountState state, SessionAsked _) =>
        state with { Session = new Session.Asking() };

    [ReducerMethod]
    public static AccountState On(AccountState state, SessionAnswered action) => state with
    {
        Session = action.Account is { } account ? new Session.In(account) : new Session.Out(),
        // The password is gone the moment it is not needed, including out of the reducer's own
        // history, rather than sitting in memory for the rest of the session.
        Draft = AccountDraft.Empty,
        // Somebody who just signed out wants to sign in again, not to make a second account.
        // Leaving the form on whichever arm they last used meant the screen after signing out
        // asked for a name and a new password.
        Form = action.Account is null ? AccountForm.SignIn : state.Form,
        Working = false,
        Trouble = null,
    };

    [ReducerMethod]
    public static AccountState On(AccountState state, AccountDraftChanged action) =>
        state with { Draft = action.Draft, Trouble = null };

    [ReducerMethod]
    public static AccountState On(AccountState state, AccountFormSwitched action) =>
        state with { Form = action.Form, Trouble = null };

    [ReducerMethod]
    public static AccountState On(AccountState state, SignInSubmitted _) =>
        state with { Working = true, Trouble = null };

    [ReducerMethod]
    public static AccountState On(AccountState state, RegistrationSubmitted _) =>
        state with { Working = true, Trouble = null };

    [ReducerMethod]
    public static AccountState On(AccountState state, AccountRefused action) =>
        state with { Working = false, Trouble = action.Message };
}
