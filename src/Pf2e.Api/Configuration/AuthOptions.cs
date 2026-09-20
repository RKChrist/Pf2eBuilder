using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Pf2e.Api.Configuration;

/// <summary>
/// One section holding both halves rather than two sections side by side, because the cookie and
/// the token it carries have to be checked against each other and a validator only ever sees the
/// object it is given.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    public AuthCookieOptions Cookie { get; set; } = new();

    public AuthJwtOptions Jwt { get; set; } = new();
}

public sealed class AuthCookieOptions
{
    public string Name { get; set; } = "pf2e.auth";

    /// <summary>How long the envelope lives. One of the three limits on a session, along with
    /// <see cref="AuthJwtOptions.AccessTokenMinutes"/> and
    /// <see cref="AuthJwtOptions.RefreshTokenMinutes"/>; a session ends when the first of them
    /// runs out.</summary>
    public int ExpireMinutes { get; set; } = 720;

    /// <summary>Renews the cookie on activity. The token inside it is renewed alongside, by
    /// <see cref="Authentication.AccountSession.KeepFreshAsync"/>, which is what makes this
    /// setting mean what its name says.</summary>
    public bool SlidingExpiration { get; set; } = true;

    /// <summary>
    /// A member of <see cref="SameSiteMode"/>, by name.
    /// <para>Lax is enough for the two-process development run. A port is not part of a site, so
    /// a client on localhost:5173 calling an API on localhost:5092 is same-site and the cookie
    /// is sent; verify-account.mjs drives exactly that and comes back signed in after a reload.
    /// None, which then also requires Secure and therefore https, is needed only when the client
    /// and the API are served from genuinely different sites.</para>
    /// </summary>
    public string SameSite { get; set; } = "Lax";

    /// <summary>A member of <see cref="CookieSecurePolicy"/>, by name. SameAsRequest rather than
    /// Always, because the table this runs for is often a laptop on plain http on the sofa.</summary>
    public string SecurePolicy { get; set; } = "SameAsRequest";

    public string LoginPath { get; set; } = "/account";

    public string AccessDeniedPath { get; set; } = "/account";
}

public sealed class AuthJwtOptions
{
    public string Issuer { get; set; } = "pf2e-builder";

    public string Audience { get; set; } = "pf2e-builder";

    /// <summary>No default on purpose. <see cref="AuthOptionsValidator"/> refuses to start
    /// without one, and Program.cs generates a throwaway for Development only.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>How long re-minting may go on before somebody has to sign in again. Measured
    /// from when the sign-in happened, not from the ticket's IssuedUtc, for the reason written
    /// on <see cref="Authentication.AccountSession.SignedInAtName"/>.</summary>
    public int RefreshTokenMinutes { get; set; } = 20160;

    public int ClockSkewSeconds { get; set; } = 30;
}

/// <summary>
/// Every failure names the setting by the path an operator would type into appsettings or a user
/// secret, because the thing a missing key costs is a process that will not start and the only
/// useful sentence at that moment is which line to add. A data annotation names the property,
/// which is not the same string.
/// </summary>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    /// <summary>HS256 refuses to build a keyed hash from a key shorter than its 256-bit hash
    /// output, so a thirty-one character key is not a weak deployment, it is an
    /// ArgumentOutOfRangeException at the first sign-in. Moving that to boot is the point of the
    /// rule. AuthOptionsRules pins the number to the library rather than to this sentence.
    /// </summary>
    public const int MinimumSigningKeyLength = 32;

    const int MaxCookieMinutes = 43200;
    const int MaxAccessTokenMinutes = 1440;
    const int MaxRefreshTokenMinutes = 525600;
    const int MaxClockSkewSeconds = 300;

    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();
        var cookie = options.Cookie;
        var jwt = options.Jwt;

        if (string.IsNullOrWhiteSpace(cookie.Name))
        {
            failures.Add($"{Path("Cookie", nameof(cookie.Name))} is required. " +
                         "Without it the framework names the cookie after the scheme instead.");
        }

        Rooted(failures, "Cookie", nameof(cookie.LoginPath), cookie.LoginPath);
        Rooted(failures, "Cookie", nameof(cookie.AccessDeniedPath), cookie.AccessDeniedPath);

        Parses<SameSiteMode>(failures, "Cookie", nameof(cookie.SameSite), cookie.SameSite);
        Parses<CookieSecurePolicy>(failures, "Cookie", nameof(cookie.SecurePolicy), cookie.SecurePolicy);

        Between(failures, "Cookie", nameof(cookie.ExpireMinutes), cookie.ExpireMinutes, 1, MaxCookieMinutes);
        Between(failures, "Jwt", nameof(jwt.AccessTokenMinutes), jwt.AccessTokenMinutes, 1, MaxAccessTokenMinutes);
        Between(failures, "Jwt", nameof(jwt.RefreshTokenMinutes), jwt.RefreshTokenMinutes, 1, MaxRefreshTokenMinutes);
        Between(failures, "Jwt", nameof(jwt.ClockSkewSeconds), jwt.ClockSkewSeconds, 0, MaxClockSkewSeconds);

        if (string.IsNullOrWhiteSpace(jwt.Issuer))
        {
            failures.Add($"{Path("Jwt", nameof(jwt.Issuer))} is required.");
        }

        if (string.IsNullOrWhiteSpace(jwt.Audience))
        {
            failures.Add($"{Path("Jwt", nameof(jwt.Audience))} is required.");
        }

        if (jwt.SigningKey.Length < MinimumSigningKeyLength)
        {
            failures.Add(
                $"{Path("Jwt", nameof(jwt.SigningKey))} is required and must be at least " +
                $"{MinimumSigningKeyLength} characters. Set it as a user secret or an environment " +
                "variable; it is the secret every session in flight is signed with, so it is never " +
                "committed and changing it signs everybody out.");
        }

        // Re-minting that stops before the first token it would replace has expired is not a
        // limit on the session, it is a gap: the token would die with renewal already switched
        // off and the sign-in would end earlier than either number says.
        if (jwt.RefreshTokenMinutes < jwt.AccessTokenMinutes)
        {
            failures.Add(
                $"{Path("Jwt", nameof(jwt.RefreshTokenMinutes))} is {jwt.RefreshTokenMinutes} and " +
                $"{Path("Jwt", nameof(jwt.AccessTokenMinutes))} is {jwt.AccessTokenMinutes}. " +
                "Re-minting has to be allowed to last at least as long as one token does.");
        }

        // The cookie is the envelope and the token is the credential inside it. A cookie that
        // expires first ends the session while the credential it carries is still good, which
        // makes the configured access-token lifetime a number that can never be reached.
        if (cookie.ExpireMinutes < jwt.AccessTokenMinutes)
        {
            failures.Add(
                $"{Path("Cookie", nameof(cookie.ExpireMinutes))} is {cookie.ExpireMinutes} and " +
                $"{Path("Jwt", nameof(jwt.AccessTokenMinutes))} is {jwt.AccessTokenMinutes}. " +
                "The cookie carries the token, so it has to last at least as long as the token does.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    static string Path(string group, string setting) => $"{AuthOptions.Section}:{group}:{setting}";

    // IsDefined as well as TryParse, because TryParse accepts any integer against any enum, so
    // "7" would pass as a SameSiteMode and become (SameSiteMode)7 in the cookie options while
    // the message underneath claimed it had to be one of the four names.
    static void Parses<TEnum>(List<string> failures, string group, string setting, string value)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            failures.Add($"{Path(group, setting)} is '{value}'. It must be one of " +
                         $"{string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }

    static void Between(List<string> failures, string group, string setting, int value, int low, int high)
    {
        if (value < low || value > high)
        {
            failures.Add($"{Path(group, setting)} is {value}. It must be between {low} and {high}.");
        }
    }

    // PathString throws on a value that does not start with a slash, and it is constructed while
    // the authentication options are being built, which is far away from the line that is wrong.
    static void Rooted(List<string> failures, string group, string setting, string value)
    {
        if (!value.StartsWith('/'))
        {
            failures.Add($"{Path(group, setting)} is '{value}'. A path starts with a slash.");
        }
    }
}
