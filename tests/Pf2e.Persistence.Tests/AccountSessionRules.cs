using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The whole pipeline in one process and no port: the routes, the cookie middleware, Data
/// Protection and the token inside the ticket. The token being load-bearing is a claim about
/// what the process does with a cookie somebody sends it, and nothing short of sending one
/// tests it.
/// <para>Production on purpose, so the key comes from configuration and not from the throwaway
/// Program.cs generates for Development.</para>
/// </summary>
public sealed class AccountsHost : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "a-test-signing-key-of-at-least-32-characters";

    public const string CookieName = "pf2e.auth";

    readonly string _file = Path.Combine(Path.GetTempPath(), $"pf2e-accounts-{Guid.NewGuid():N}.db");

    public Task InitializeAsync()
    {
        // Forces the host to be built and the migrations to run before the first test, so a
        // failure there reads as a failure there.
        _ = Services;
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Auth:Jwt:SigningKey"] = SigningKey,
                ["Database:ConnectionString"] = $"Data Source={_file}",

                // This suite is about sessions. Seeding the ruleset would add twenty seconds and
                // prove nothing here, and SeededDatabase already covers it.
                ["Seeding:Enabled"] = "false",
            }));
    }

    /// <summary>
    /// Writes a cookie the way the running process would, so a test can put a ticket the
    /// middleware accepts around a token it must not. Anything less than the real protector
    /// would test this file's idea of the cookie format.
    /// </summary>
    public string Protect(AuthenticationTicket ticket) =>
        Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme)
                .TicketDataFormat!
                .Protect(ticket);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnectionPool.Clear();
        File.Delete(_file);
        GC.SuppressFinalize(this);
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

public class AccountSessionRules(AccountsHost host) : IClassFixture<AccountsHost>
{
    const string Password = "a-long-enough-passphrase";

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Cookies are carried by hand rather than by a cookie container, because two of
    /// these tests are about a cookie the browser was never given and one is about the cookie
    /// coming back cleared.</summary>
    HttpClient Client() => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false,
    });

    static string FreshEmail() => $"gnibbo+{Guid.NewGuid():N}@example.test";

    /// <summary>Empty when the response clears the cookie, null when it does not mention it.</summary>
    static string? CookieIn(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            return null;
        }

        var header = headers.FirstOrDefault(
            h => h.StartsWith($"{AccountsHost.CookieName}=", StringComparison.Ordinal));
        if (header is null)
        {
            return null;
        }

        var value = header[(AccountsHost.CookieName.Length + 1)..];
        var end = value.IndexOf(';', StringComparison.Ordinal);

        return end < 0 ? value : value[..end];
    }

    static async Task<SessionView> SessionOn(HttpClient client, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/accounts/session");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{AccountsHost.CookieName}={cookie}");
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionView>(Json))!;
    }

    async Task<(AccountView Account, string Cookie)> Registered(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/accounts", new RegisterAccountRequest(FreshEmail(), "Gnibbo", Password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = (await response.Content.ReadFromJsonAsync<AccountView>(Json))!;
        var cookie = CookieIn(response);

        Assert.False(string.IsNullOrEmpty(cookie), "Registering did not set a cookie.");
        return (account, cookie!);
    }

    /// <summary>
    /// A ticket the cookie middleware will happily decrypt, around a token this process will
    /// not accept. The name in the identity is the real one and the token's subject is the real
    /// account, so an implementation that trusted either would answer as the account, which is
    /// exactly the mistake these tests exist to catch.
    /// </summary>
    string TicketCarrying(string token, string displayName)
    {
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
        };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, displayName)],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return host.Protect(new AuthenticationTicket(
            new ClaimsPrincipal(identity), properties,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    static string Token(Guid accountId, string signingKey, DateTime expires) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "pf2e-builder",
            Audience = "pf2e-builder",
            Claims = new Dictionary<string, object> { ["sub"] = accountId.ToString() },
            IssuedAt = DateTime.UtcNow.AddMinutes(-5),
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        });

    [Fact]
    public async Task ABrowserThatHasNeverSignedInIsNobodyRatherThanRefused()
    {
        using var client = Client();

        Assert.Null((await SessionOn(client, cookie: null)).Account);
    }

    [Fact]
    public async Task RegisteringSignsYouInAndTheSessionComesBackFromTheTokenInTheCookie()
    {
        using var client = Client();
        var (account, cookie) = await Registered(client);

        var session = await SessionOn(client, cookie);

        Assert.NotNull(session.Account);
        Assert.Equal(account.Id, session.Account.Id);
        Assert.Equal("Gnibbo", session.Account.DisplayName);
    }

    [Fact]
    public async Task TheCookieIsHttpOnlySoScriptCannotReachTheTokenInsideIt()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync(
            "/accounts", new RegisterAccountRequest(FreshEmail(), "Gnibbo", Password));
        var header = response.Headers.GetValues("Set-Cookie")
                             .First(h => h.StartsWith(AccountsHost.CookieName, StringComparison.Ordinal));

        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asked of the token library rather than by looking for a substring. A hand-rolled search
    /// for the base64 of the subject proves nothing: a JWT is base64url, not base64, and whether
    /// an encoded fragment appears at all depends on its byte offset inside the payload, so such
    /// a test passes by alignment luck even against a cookie that is the raw token.
    /// </summary>
    [Fact]
    public async Task WhatTheBrowserReceivesIsNotAReadableTokenAtAll()
    {
        using var client = Client();
        var (account, cookie) = await Registered(client);
        var reader = new JsonWebTokenHandler();

        // That this assertion can fail at all, proved here rather than assumed: the same call
        // says yes to an actual token, so a cookie that were one would not slip through.
        Assert.True(
            reader.CanReadToken(Token(account.Id, AccountsHost.SigningKey, DateTime.UtcNow.AddHours(1))),
            "The check cannot recognise a token, so its refusal of the cookie means nothing.");

        Assert.False(
            reader.CanReadToken(cookie),
            "The cookie is a token the browser could read.");

        // The ticket is one Data Protection blob, so it has none of a JWT's dot-separated
        // segments to pull a payload out of either.
        Assert.False(
            cookie.Split('.').Length >= 3,
            "The cookie is shaped like a token even if this library will not read it.");
    }

    /// <summary>
    /// The one the whole design rests on. The cookie decrypts, the ticket is well formed, the
    /// name in it is real and the token names the real account, so anything that answered from
    /// the ticket rather than from the signature would answer as Gnibbo.
    /// </summary>
    [Fact]
    public async Task ATokenThisProcessDidNotSignIsNobody()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var forged = TicketCarrying(
            Token(account.Id, "a-different-key-of-at-least-32-characters", DateTime.UtcNow.AddHours(1)),
            account.DisplayName);

        Assert.Null((await SessionOn(client, forged)).Account);
    }

    [Fact]
    public async Task ATokenThisProcessDidNotSignAlsoTakesTheCookieAwayRatherThanLeavingItToFailForever()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var forged = TicketCarrying(
            Token(account.Id, "a-different-key-of-at-least-32-characters", DateTime.UtcNow.AddHours(1)),
            account.DisplayName);

        var request = new HttpRequestMessage(HttpMethod.Get, "/accounts/session");
        request.Headers.Add("Cookie", $"{AccountsHost.CookieName}={forged}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, CookieIn(response));
    }

    [Fact]
    public async Task AnExpiredTokenIsNobodyEvenInsideACookieThatIsStillGood()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var stale = TicketCarrying(
            Token(account.Id, AccountsHost.SigningKey, DateTime.UtcNow.AddMinutes(-30)),
            account.DisplayName);

        Assert.Null((await SessionOn(client, stale)).Account);
    }

    [Fact]
    public async Task ATicketWithNoTokenInItAtAllIsNobody()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) };
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, account.DisplayName)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        var empty = host.Protect(new AuthenticationTicket(
            new ClaimsPrincipal(identity), properties,
            CookieAuthenticationDefaults.AuthenticationScheme));

        Assert.Null((await SessionOn(client, empty)).Account);
    }

    /// <summary>A subject that names nobody is still an answer of nobody. It is worth its own
    /// test because the query behind this route refuses an empty id, so the route would answer
    /// 400 rather than 200 if the boundary did not stop it first.</summary>
    [Fact]
    public async Task AProperlySignedTokenThatNamesNobodyIsStillTwoHundredAndNobody()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var empty = TicketCarrying(
            Token(Guid.Empty, AccountsHost.SigningKey, DateTime.UtcNow.AddHours(1)),
            account.DisplayName);

        Assert.Null((await SessionOn(client, empty)).Account);
    }

    [Fact]
    public async Task SigningInAgainOnAnotherDeviceAnswersTheSameAccount()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var response = await client.PostAsJsonAsync(
            "/accounts/session", new SignInRequest(account.Email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = CookieIn(response);
        Assert.False(string.IsNullOrEmpty(cookie));

        var session = await SessionOn(client, cookie);
        Assert.Equal(account.Id, session.Account!.Id);
    }

    [Fact]
    public async Task SigningOutAnswersNoContentAndTellsTheBrowserToDropTheCookie()
    {
        using var client = Client();
        var (_, cookie) = await Registered(client);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/accounts/session");
        request.Headers.Add("Cookie", $"{AccountsHost.CookieName}={cookie}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, CookieIn(response));
    }

    /// <summary>
    /// The limit of a stateless sign-out, asserted rather than assumed. Signing out tells the
    /// browser to drop its cookie and nothing else, so a copy of that cookie taken beforehand
    /// still answers as the account until the token inside it expires, which is
    /// Auth:Jwt:AccessTokenMinutes away. This is not a defect being papered over, it is the
    /// trade a cookie with no server-side store makes, and it is written down here so that
    /// nobody reads the test above as proof of something stronger. Ending it on the server would
    /// need the token's id recorded and checked on every read.
    /// </summary>
    [Fact]
    public async Task ACopyOfTheCookieTakenBeforeSigningOutStillWorksAfterwards()
    {
        using var client = Client();
        var (account, cookie) = await Registered(client);

        var signOut = new HttpRequestMessage(HttpMethod.Delete, "/accounts/session");
        signOut.Headers.Add("Cookie", $"{AccountsHost.CookieName}={cookie}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(signOut)).StatusCode);

        var replayed = await SessionOn(client, cookie);

        Assert.NotNull(replayed.Account);
        Assert.Equal(account.Id, replayed.Account.Id);
    }

    [Fact]
    public async Task AnAddressThatIsTakenIsAConflictAndNotAFault()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var response = await client.PostAsJsonAsync(
            "/accounts", new RegisterAccountRequest(account.Email, "Somebody Else", Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownAddressAndAWrongPasswordAreBothARefusalInTheSameWords()
    {
        using var client = Client();
        var (account, _) = await Registered(client);

        var wrongPassword = await client.PostAsJsonAsync(
            "/accounts/session", new SignInRequest(account.Email, "a-different-passphrase"));
        var unknownAddress = await client.PostAsJsonAsync(
            "/accounts/session", new SignInRequest(FreshEmail(), Password));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAddress.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownAddress.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AShortPasswordIsARefusalOfTheRequestAndNotOfTheCredentials()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync(
            "/accounts", new RegisterAccountRequest(FreshEmail(), "Gnibbo", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The constraint the seven browser verifiers rest on. Creating a campaign is the first thing
    /// every one of them does, from a browser that has never heard of an account.
    /// </summary>
    [Fact]
    public async Task ACampaignIsStillCreatedByABrowserWithNoCookieAtAll()
    {
        using var client = Client();

        var response = await client.PostAsync("/campaigns", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
