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

    /// <summary>
    /// How long the cookie lives. Not how long a session lasts, and today that difference is
    /// real. The token inside the cookie is minted once at sign-in and never re-minted, and a
    /// session read signs the browser out the moment that token expires, so the session ends
    /// after <see cref="AuthJwtOptions.AccessTokenMinutes"/> however long the cookie was told to
    /// live. With the shipped defaults that is one hour, not twelve.
    /// <para>Closing the gap means re-minting on a sliding renewal, which is what
    /// <see cref="AuthJwtOptions.RefreshTokenMinutes"/> is there for and what nothing does yet.
    /// Until then, treat this and <see cref="SlidingExpiration"/> as an upper bound the process
    /// does not reach.</para>
    /// </summary>
    public int ExpireMinutes { get; set; } = 720;

    /// <summary>Renews the cookie on activity. It does not renew the token inside it, so see
    /// <see cref="ExpireMinutes"/> for what that is worth today.</summary>
    public bool SlidingExpiration { get; set; } = true;

    /// <summary>A member of <see cref="SameSiteMode"/>, by name.</summary>
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

    /// <summary>Configured ahead of the thing that will read it. Nothing mints or honours a
    /// refresh token yet, so today this is validated and read by nothing. See
    /// <see cref="AuthCookieOptions.ExpireMinutes"/> for what its absence currently costs.
    /// </summary>
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
