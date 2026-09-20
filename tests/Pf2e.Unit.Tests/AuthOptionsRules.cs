using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pf2e.Api.Configuration;

namespace Pf2e.Unit.Tests;

/// <summary>
/// What the process refuses to start without. These assert on the message and not only on the
/// failure, because the whole reason this is a validator rather than a data annotation is that
/// somebody reading a crashed boot log has to be told which line of configuration to add.
/// </summary>
public class AuthOptionsRules
{
    const string Key = "a-signing-key-of-at-least-32-characters";

    static AuthOptions Complete() => new()
    {
        Jwt = new AuthJwtOptions { SigningKey = Key },
    };

    static ValidateOptionsResult Check(AuthOptions options) =>
        new AuthOptionsValidator().Validate(AuthOptions.Section, options);

    static string RefusalOf(AuthOptions options)
    {
        var result = Check(options);

        Assert.True(result.Failed, "The configuration was accepted.");
        return result.FailureMessage ?? string.Empty;
    }

    [Fact]
    public void ACompleteConfigurationPasses()
    {
        var result = Check(Complete());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void AMissingSigningKeyIsRefusedAndTheRefusalNamesTheSettingToSet()
    {
        Assert.Contains("Auth:Jwt:SigningKey", RefusalOf(new AuthOptions()));
    }

    [Fact]
    public void ASigningKeyShorterThanTheSignatureNeedsIsRefusedLikeAMissingOne()
    {
        var options = Complete();
        options.Jwt.SigningKey = new string('k', AuthOptionsValidator.MinimumSigningKeyLength - 1);

        Assert.Contains("Auth:Jwt:SigningKey", RefusalOf(options));
    }

    [Fact]
    public void ACookieThatExpiresBeforeTheTokenItCarriesIsRefusedAndBothSettingsAreNamed()
    {
        var options = Complete();
        options.Cookie.ExpireMinutes = 30;
        options.Jwt.AccessTokenMinutes = 60;

        var refusal = RefusalOf(options);

        Assert.Contains("Auth:Cookie:ExpireMinutes", refusal);
        Assert.Contains("Auth:Jwt:AccessTokenMinutes", refusal);
    }

    /// <summary>Renewal that stops before the first token it would replace has expired is not a
    /// limit on the session, it is a gap: the token dies with renewal already switched off and
    /// the sign-in ends earlier than either setting says.</summary>
    [Fact]
    public void RenewingForLessTimeThanOneTokenLastsIsRefusedAndBothSettingsAreNamed()
    {
        var options = Complete();
        options.Jwt.AccessTokenMinutes = 60;
        options.Jwt.RefreshTokenMinutes = 30;

        var refusal = RefusalOf(options);

        Assert.Contains("Auth:Jwt:RefreshTokenMinutes", refusal);
        Assert.Contains("Auth:Jwt:AccessTokenMinutes", refusal);
    }

    [Fact]
    public void RenewingForExactlyOneTokensLifetimeIsAllowed()
    {
        var options = Complete();
        options.Jwt.AccessTokenMinutes = 60;
        options.Jwt.RefreshTokenMinutes = 60;

        Assert.True(Check(options).Succeeded);
    }

    [Fact]
    public void ACookieThatOutlivesTheTokenItCarriesIsAllowed()
    {
        var options = Complete();
        options.Cookie.ExpireMinutes = 720;
        options.Jwt.AccessTokenMinutes = 60;

        Assert.True(Check(options).Succeeded);
    }

    [Theory]
    [InlineData("Lax")]
    [InlineData("strict")]
    [InlineData("None")]
    public void ASameSiteTheFrameworkKnowsIsAccepted(string named)
    {
        var options = Complete();
        options.Cookie.SameSite = named;

        Assert.True(Check(options).Succeeded);
    }

    [Fact]
    public void ASameSiteTheFrameworkDoesNotKnowIsRefusedWithTheNamesItDoes()
    {
        var options = Complete();
        options.Cookie.SameSite = "Loose";

        var refusal = RefusalOf(options);

        Assert.Contains("Auth:Cookie:SameSite", refusal);
        Assert.Contains("Strict", refusal);
    }

    /// <summary>A number is not one of the names. Enum.TryParse takes any integer against any
    /// enum, so without a defined-ness check this passes and becomes (SameSiteMode)7 on the
    /// cookie, which is neither Lax nor Strict nor None.</summary>
    [Fact]
    public void ANumberIsNotASameSiteEvenThoughItParsesAsOne()
    {
        var options = Complete();
        options.Cookie.SameSite = "7";

        Assert.Contains("Auth:Cookie:SameSite", RefusalOf(options));
    }

    [Fact]
    public void ANumberIsNotASecurePolicyEither()
    {
        var options = Complete();
        options.Cookie.SecurePolicy = "99";

        Assert.Contains("Auth:Cookie:SecurePolicy", RefusalOf(options));
    }

    [Fact]
    public void ASecurePolicyTheFrameworkDoesNotKnowIsRefused()
    {
        var options = Complete();
        options.Cookie.SecurePolicy = "Sometimes";

        Assert.Contains("Auth:Cookie:SecurePolicy", RefusalOf(options));
    }

    [Fact]
    public void ALoginPathThatIsNotAPathIsRefusedHereRatherThanThrownAtWiringTime()
    {
        var options = Complete();
        options.Cookie.LoginPath = "account";

        Assert.Contains("Auth:Cookie:LoginPath", RefusalOf(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATokenLifetimeOutsideItsRangeIsRefused(int minutes)
    {
        var options = Complete();
        options.Jwt.AccessTokenMinutes = minutes;

        Assert.Contains("Auth:Jwt:AccessTokenMinutes", RefusalOf(options));
    }

    /// <summary>
    /// Ties the validator's constant to what the signing library actually does, because the
    /// reason for the number is not obvious and a plausible wrong reason is written down in
    /// several places elsewhere. Microsoft.IdentityModel advertises a minimum symmetric key of
    /// 128 bits, which is the check that produces IDX10653, and HS256 then refuses anything
    /// under its 256-bit hash output when the keyed hash is built, which is IDX10720. So a key
    /// between the two is not weak, it throws, and it throws at the first sign-in rather than at
    /// boot. That is the failure the validator is moving forward in time.
    /// </summary>
    [Fact]
    public void AKeyOneCharacterUnderTheMinimumCannotSignAtAll()
    {
        var tooShort = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(new string('k', AuthOptionsValidator.MinimumSigningKeyLength - 1)));

        Assert.Throws<ArgumentOutOfRangeException>(() => Sign(tooShort));
    }

    [Fact]
    public void AKeyAtTheMinimumSigns()
    {
        var enough = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(new string('k', AuthOptionsValidator.MinimumSigningKeyLength)));

        Assert.False(string.IsNullOrEmpty(Sign(enough)));
    }

    static string Sign(SymmetricSecurityKey key) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "pf2e-builder",
            Audience = "pf2e-builder",
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString() },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });

    [Fact]
    public void EverythingThatIsWrongIsReportedAtOnceRatherThanOnePerBoot()
    {
        var refusal = RefusalOf(new AuthOptions { Cookie = new AuthCookieOptions { SameSite = "Loose" } });

        Assert.Contains("Auth:Jwt:SigningKey", refusal);
        Assert.Contains("Auth:Cookie:SameSite", refusal);
    }
}
