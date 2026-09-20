using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pf2e.Api.Authentication;
using Pf2e.Api.Configuration;
using Pf2e.Application.Features.Accounts;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Api.Endpoints;

/// <summary>
/// Accounts sit underneath the campaign code and the DM key and replace neither. Nothing here
/// gates a campaign route, none of those routes reads a cookie, and a signed-out browser can
/// still do everything it could before: a code buys a seat and the DM key buys the fight,
/// whether or not the browser holding them has a name.
/// </summary>
public static class AccountEndpoints
{
    const string Scheme = AccountSession.Scheme;

    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder app)
    {
        // Registering signs you in, because the alternative is a form that succeeds and then
        // asks for the password it was just given.
        app.MapPost("/accounts", async (
            ISender sender, HttpContext context, IOptions<AuthOptions> auth,
            RegisterAccountRequest body, CancellationToken ct) =>
        {
            var account = await sender.Send(
                new RegisterAccount(body.Email, body.DisplayName, body.Password), ct);

            await GrantAsync(context, auth.Value, account);

            // No Location: an account is only ever read as a session, and a header naming a
            // route that does not exist is worse than no header.
            return Results.Created((string?)null, account);
        });

        app.MapPost("/accounts/session", async (
            ISender sender, HttpContext context, IOptions<AuthOptions> auth,
            SignInRequest body, CancellationToken ct) =>
        {
            var account = await sender.Send(new SignIn(body.Email, body.Password), ct);

            await GrantAsync(context, auth.Value, account);
            return Results.Ok(account);
        });

        app.MapDelete("/accounts/session", async (HttpContext context) =>
        {
            await context.SignOutAsync(Scheme);
            return Results.NoContent();
        });

        // Always 200, never a refusal. "Who is this browser" has a legitimate answer of nobody,
        // and nothing was denied. The client asks it on every page load so the header can say a
        // name, and a 4xx there would make Chrome log a failed request on every screen for every
        // signed-out visitor, which the browser verifiers read as a console error.
        app.MapGet("/accounts/session", async (
            ISender sender, HttpContext context, IOptions<AuthOptions> auth, CancellationToken ct) =>
            Results.Ok(new SessionView(await WhoAsync(sender, context, auth.Value, ct))));

        return app;
    }

    /// <summary>
    /// Answered from the token in the ticket and not from the identity in the cookie. The
    /// identity is whatever was written into it; the token is checked against the signing key,
    /// the issuer, the audience and the clock. That is what makes it load-bearing rather than
    /// decoration.
    /// <para>Checked here even though <see cref="AccountSession.KeepFreshAsync"/> has already
    /// checked it on the way in, and deliberately so. That one is asking whether the ticket is
    /// still healthy; this one is asking who it names. Keeping the second question answered from
    /// the signature is what leaves the account id in exactly one place, so no later edit can
    /// reach for a cheaper answer that happens to be sitting in the principal.</para>
    /// <para>A token that fails has already had its cookie cleared by that handler. What reaches
    /// the sign-out below is the case it cannot see: a token that is perfectly good and names an
    /// account that is no longer there.</para>
    /// </summary>
    static async Task<AccountView?> WhoAsync(
        ISender sender, HttpContext context, AuthOptions auth, CancellationToken ct)
    {
        var ticket = await context.AuthenticateAsync(Scheme);
        if (!ticket.Succeeded)
        {
            return null;
        }

        var account = await AccountOfAsync(sender, ticket, auth.Jwt, ct);
        if (account is null)
        {
            await context.SignOutAsync(Scheme);
        }

        return account;
    }

    static async Task<AccountView?> AccountOfAsync(
        ISender sender, AuthenticateResult ticket, AuthJwtOptions jwt, CancellationToken ct)
    {
        var token = AccountSession.TokenIn(ticket.Properties);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var accountId = AccountSession.AccountIn(await AccountSession.ValidateAsync(token, jwt));

        return accountId is null ? null : await sender.Send(new GetAccount(accountId.Value), ct);
    }

    static async Task GrantAsync(HttpContext context, AuthOptions auth, AccountView account)
    {
        var properties = new AuthenticationProperties();
        AccountSession.Grant(properties, auth.Jwt, account.Id);

        // A name and no id. The account id exists in exactly one place, the signed token, so
        // there is no shortcut for a later reader to take: nothing can learn which account this
        // is without putting the token through the same checks first.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, account.DisplayName)], Scheme);

        await context.SignInAsync(Scheme, new ClaimsPrincipal(identity), properties);
    }
}
