using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pf2e.Api.Configuration;
using System.Text;

namespace Pf2e.Api.Authentication;

/// <summary>
/// The signed token that lives inside the authentication cookie. How it is minted, how it is
/// checked, and how it is kept alive while somebody is still at the table.
/// <para>Three settings and three separate jobs. <c>Auth:Jwt:AccessTokenMinutes</c> is how long
/// one token is good for. <c>Auth:Jwt:RefreshTokenMinutes</c> is how long re-minting may go on
/// before somebody has to sign in again. <c>Auth:Cookie:ExpireMinutes</c> is how long the
/// envelope exists at all. A session ends at whichever of the three runs out first.</para>
/// </summary>
public static class AccountSession
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// What the signed token is stored under inside the ticket. The browser never receives a
    /// readable one: the ticket is encrypted by Data Protection before it becomes a cookie, and
    /// that cookie is HttpOnly, so a tab holds an opaque blob it can neither read nor edit nor
    /// hand to script.
    /// </summary>
    public const string TokenName = "access_token";

    /// <summary>
    /// When this sign-in happened, written once and never rewritten, because it is the only
    /// thing that can bound re-minting.
    /// <para>The ticket's own <see cref="AuthenticationProperties.IssuedUtc"/> cannot do it. The
    /// cookie handler calls RequestRefresh whenever a ticket is renewed, which stamps IssuedUtc
    /// with the current time, and that includes every renewal made below. A cap measured from it
    /// would move forward each time it was tested and <c>RefreshTokenMinutes</c> would never be
    /// reached, so the session would last forever. AccountSessionRules asserts that difference
    /// rather than leaving this paragraph to be believed.</para>
    /// </summary>
    public const string SignedInAtName = "signed_in_utc";

    /// <summary>Puts a freshly minted token and the sign-in stamp into a new ticket's
    /// properties. The one place a session begins.</summary>
    public static void Grant(AuthenticationProperties properties, AuthJwtOptions jwt, Guid accountId)
    {
        var now = DateTime.UtcNow;

        Carry(properties, Mint(jwt, accountId, now));
        properties.Items[SignedInAtName] = now.ToString("O", CultureInfo.InvariantCulture);
    }

    public static void Carry(AuthenticationProperties properties, string token) =>
        properties.StoreTokens([new AuthenticationToken { Name = TokenName, Value = token }]);

    public static string? TokenIn(AuthenticationProperties? properties) =>
        properties?.GetTokenValue(TokenName);

    public static DateTime? SignedInAt(AuthenticationProperties? properties) =>
        properties is not null
        && properties.Items.TryGetValue(SignedInAtName, out var stamp)
        && DateTime.TryParse(
            stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at.ToUniversalTime()
            : null;

    public static Task<TokenValidationResult> ValidateAsync(string token, AuthJwtOptions jwt) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, Checks(jwt));

    /// <summary>
    /// The account a validated token names, or null when it names none. The empty Guid is
    /// refused here, at the boundary the token arrives at, because the routes downstream treat
    /// "no account" as an answer and a meaningless subject must not become an error instead.
    /// </summary>
    public static Guid? AccountIn(TokenValidationResult validated) =>
        validated.IsValid
        && validated.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var subject)
        && Guid.TryParse(subject as string, out var accountId)
        && accountId != Guid.Empty
            ? accountId
            : null;

    /// <summary>
    /// Runs on every request that carries the cookie, and does two things, neither of which is
    /// deciding who you are.
    /// <para>It throws out a ticket whose token no longer holds. Rejecting alone would leave the
    /// cookie in the browser to be presented and refused again on every request until the cookie
    /// itself expired, so the browser is signed out as well and stops carrying it.</para>
    /// <para>And it re-mints while somebody is still here. A table runs three to five hours; a
    /// token good for one and never renewed would sign the GM out in the middle of a fight, with
    /// nothing in the configuration to explain why, while the cookie's own settings promised
    /// twelve hours. Renewal is what makes <c>SlidingExpiration</c> mean anything.</para>
    /// </summary>
    public static async Task KeepFreshAsync(CookieValidatePrincipalContext context, AuthOptions auth)
    {
        var jwt = auth.Jwt;
        var token = TokenIn(context.Properties);

        var validated = string.IsNullOrEmpty(token) ? null : await ValidateAsync(token, jwt);
        var accountId = validated is null ? null : AccountIn(validated);

        if (accountId is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
            return;
        }

        var now = DateTime.UtcNow;

        // Renewed only in the last half of the token's life. Re-minting on every request would
        // rewrite the cookie on every page load for no gain, and would make the token's stated
        // lifetime meaningless by never letting one get old.
        if (validated!.SecurityToken.ValidTo - now > TimeSpan.FromMinutes(jwt.AccessTokenMinutes) / 2)
        {
            return;
        }

        var signedInAt = SignedInAt(context.Properties);

        // Past the point where re-minting may continue. Nothing is rejected here on purpose: the
        // token already in the ticket stays good until it expires, so the session ends when it
        // does rather than being cut off mid-request. That is what makes RefreshTokenMinutes a
        // limit on how long a sign-in lasts rather than a second way to be thrown out.
        if (signedInAt is null || now - signedInAt.Value > TimeSpan.FromMinutes(jwt.RefreshTokenMinutes))
        {
            return;
        }

        Carry(context.Properties, Mint(jwt, accountId.Value, now));
        context.ShouldRenew = true;
    }

    /// <summary>The account id as sub and nothing else. A display name in a token is a copy of a
    /// field that goes stale the moment somebody renames themselves, and the account is read
    /// fresh on every session check anyway.</summary>
    public static string Mint(AuthJwtOptions jwt, Guid accountId, DateTime now) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
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
