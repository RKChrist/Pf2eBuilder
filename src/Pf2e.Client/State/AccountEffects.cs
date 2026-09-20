using Fluxor;
using Pf2e.Client.Api;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Client.State;

public sealed class AccountEffects(AccountsApi api, IState<AccountState> state)
{
    [EffectMethod]
    public async Task Handle(SessionAsked _, IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new SessionAnswered(await api.WhoAmIAsync(CancellationToken.None)));
        }
        catch (AccountApiException failure)
        {
            // A browser that cannot reach the service is not signed out, but nothing here can
            // tell the difference, and a screen that says "sign in" is the honest fallback.
            dispatcher.Dispatch(new SessionAnswered(null));
            dispatcher.Dispatch(new AccountRefused(failure.Message));
        }
    }

    [EffectMethod]
    public Task Handle(SignInSubmitted _, IDispatcher dispatcher)
    {
        var draft = state.Value.Draft;
        return AnswerAsync(
            dispatcher,
            () => api.SignInAsync(new SignInRequest(draft.Email.Trim(), draft.Password), CancellationToken.None));
    }

    [EffectMethod]
    public Task Handle(RegistrationSubmitted _, IDispatcher dispatcher)
    {
        var draft = state.Value.Draft;
        return AnswerAsync(
            dispatcher,
            () => api.RegisterAsync(
                new RegisterAccountRequest(draft.Email.Trim(), draft.DisplayName.Trim(), draft.Password),
                CancellationToken.None));
    }

    [EffectMethod]
    public async Task Handle(SignOutRequested _, IDispatcher dispatcher)
    {
        try
        {
            await api.SignOutAsync(CancellationToken.None);
            dispatcher.Dispatch(new SessionAnswered(null));
        }
        catch (AccountApiException failure)
        {
            dispatcher.Dispatch(new AccountRefused(failure.Message));
        }
    }

    static async Task AnswerAsync(IDispatcher dispatcher, Func<Task<AccountView>> submit)
    {
        try
        {
            dispatcher.Dispatch(new SessionAnswered(await submit()));
        }
        catch (AccountApiException failure)
        {
            dispatcher.Dispatch(new AccountRefused(failure.Message));
        }
    }
}
