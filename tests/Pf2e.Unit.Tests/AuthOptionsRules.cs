using Microsoft.Extensions.Options;
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

    [Fact]
    public void EverythingThatIsWrongIsReportedAtOnceRatherThanOnePerBoot()
    {
        var refusal = RefusalOf(new AuthOptions { Cookie = new AuthCookieOptions { SameSite = "Loose" } });

        Assert.Contains("Auth:Jwt:SigningKey", refusal);
        Assert.Contains("Auth:Cookie:SameSite", refusal);
    }
}
