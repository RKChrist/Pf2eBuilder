using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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
    const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// What the signed token is stored under inside the authentication ticket. The browser never
    /// receives a readable one: the ticket is encrypted by Data Protection before it becomes a
    /// cookie, and that cookie is HttpOnly, so a tab holds an opaque blob it can neither read
    /// nor edit nor hand to script.
    /// </summary>
    const string StoredToken = "access_token";

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
    /// the issuer, the audience and the clock on every single read. That is what makes it
    /// load-bearing rather than decoration.
    /// <para>A ticket this cannot answer from signs the browser out on the way to answering
    /// nobody, because a tab holding a token that is expired, tampered with or signed with a key
    /// this process no longer has would present it again on every page load until the cookie
    /// itself expired. That covers a ticket that decrypts. One that does not, which is what a
    /// changed Data Protection key ring leaves behind, fails before this and is not cleared
    /// here; the browser carries it until <c>Auth:Cookie:ExpireMinutes</c> is up.</para>
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
        var token = ticket.Properties?.GetTokenValue(StoredToken);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var validated = await new JsonWebTokenHandler().ValidateTokenAsync(token, Checks(jwt));

        // The empty Guid is stopped here and not left to GetAccountValidator, which refuses it.
        // This is the boundary the token arrives at, and the route above has no way to say no:
        // letting a validator refuse a meaningless subject would turn it into a 400 on the one
        // route that must always answer 200.
        return validated.IsValid
               && validated.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subject)
               && Guid.TryParse(subject as string, out var accountId)
               && accountId != Guid.Empty
            ? await sender.Send(new GetAccount(accountId), ct)
            : null;
    }

    static async Task GrantAsync(HttpContext context, AuthOptions auth, AccountView account)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([
            new AuthenticationToken { Name = StoredToken, Value = Mint(auth.Jwt, account.Id) },
        ]);

        // A name and no id. The account id exists in exactly one place, the signed token, so
        // there is no shortcut for a later reader to take: nothing can learn which account this
        // is without putting the token through Checks first.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, account.DisplayName)], Scheme);

        await context.SignInAsync(Scheme, new ClaimsPrincipal(identity), properties);
    }

    /// <summary>The account id as sub and nothing else. A display name in a token is a copy of a
    /// field that goes stale the moment somebody renames themselves, and the account is read
    /// fresh on every session check anyway.</summary>
    static string Mint(AuthJwtOptions jwt, Guid accountId)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = accountId.ToString(),
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(jwt.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(KeyOf(jwt), SecurityAlgorithms.HmacSha256),
        });
    }

    static TokenValidationParameters Checks(AuthJwtOptions jwt) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = KeyOf(jwt),
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
    };

    // Safe only because AuthOptionsValidator refuses to start on a key under thirty-two
    // characters. HS256 throws rather than signing weakly below its 256-bit hash output, so
    // without that rule this line would be the one that failed, at the first sign-in.
    static SymmetricSecurityKey KeyOf(AuthJwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey));
}
