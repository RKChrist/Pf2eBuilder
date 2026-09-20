using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pf2e.Api.Authentication;
using Pf2e.Api.Configuration;
using Pf2e.Api.Endpoints;
using Pf2e.Api.Hubs;
using Pf2e.Application;
using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Accounts;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Infrastructure;
using Pf2e.Infrastructure.Configuration;
using Pf2e.Infrastructure.Persistence;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

// Read where the published client lives before the host exists, because
// UseBlazorFrameworkFiles serves _framework from the web root and ignores any file
// provider handed to UseStaticFiles. Making the client the web root is the only way
// both the framework files and the static assets come from the same place.
var bootstrap = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var clientRoot = bootstrap[$"{HostingOptions.Section}:{nameof(HostingOptions.ClientRoot)}"];

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = string.IsNullOrWhiteSpace(clientRoot) ? null : Path.GetFullPath(clientRoot),
});

// The signing key is required, and outside Development that is the point: a process that starts
// without one hands out sessions nobody configured. Inside Development it would mean a fresh
// clone refusing to start until its author finds the user-secrets incantation, which is a worse
// trade than a key that does not survive a restart. Generated here rather than defaulted in
// AuthJwtOptions, so the refusal stays real everywhere else. A new provider rather than a write
// through the existing ones, because last provider wins and this one must not beat a key the
// operator did set, which is what the guard above it is for.
var signingKeyPath = $"{AuthOptions.Section}:Jwt:{nameof(AuthJwtOptions.SigningKey)}";
var generatedSigningKey = builder.Environment.IsDevelopment()
                          && string.IsNullOrWhiteSpace(builder.Configuration[signingKeyPath]);
if (generatedSigningKey)
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [signingKeyPath] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

builder.Services.AddSection<CorsOptions>(builder.Configuration, CorsOptions.Section);
builder.Services.AddCors();

// Built from our own bound options rather than from configuration directly, so the
// post-configure binding that lets appsettings beat a library default still applies.
// AllowCredentials is what the SignalR handshake needs from a cross-origin client, and it is
// why the origin list stays explicit: a wildcard and credentials cannot both hold.
builder.Services.AddOptions<AspNetCorsOptions>()
    .Configure<IOptions<CorsOptions>>((aspnet, mine) => aspnet.AddDefaultPolicy(policy =>
        policy.WithOrigins([.. mine.Value.AllowedOrigins])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddSection<HostingOptions>(builder.Configuration, HostingOptions.Section);
builder.Services.AddSection<RealtimeOptions>(builder.Configuration, RealtimeOptions.Section);
builder.Services.AddSignalR();
builder.Services.AddOptions<HubOptions>()
    .Configure<IOptions<RealtimeOptions>>((hub, mine) =>
    {
        hub.KeepAliveInterval = TimeSpan.FromSeconds(mine.Value.KeepAliveSeconds);
        hub.ClientTimeoutInterval = TimeSpan.FromSeconds(mine.Value.ClientTimeoutSeconds);
    });
builder.Services.AddScoped<ICampaignBroadcaster, CampaignBroadcaster>();

builder.Services.AddSection<AuthOptions>(builder.Configuration, AuthOptions.Section);
builder.Services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

// Accounts sit underneath the campaign code and the DM key rather than beside them: no route
// that existed before this asks who you are, and none of them is given an authorization policy
// here. Signing in adds a name to the header and nothing else.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddAuthorization();

// Same shape as HubOptions above: our own bound section projected onto the framework's options
// in the Configure stage, which runs before the PostConfigure that fills in the library's own
// defaults, so those only ever fill what we left alone.
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>((framework, mine) =>
    {
        var cookie = mine.Value.Cookie;

        framework.Cookie.Name = cookie.Name;
        framework.Cookie.SameSite = Enum.Parse<SameSiteMode>(cookie.SameSite, ignoreCase: true);
        framework.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(cookie.SecurePolicy, ignoreCase: true);

        // The ticket carries a signed token, so script must never be able to read the cookie
        // that holds it. This is not configurable for the same reason it is not optional.
        framework.Cookie.HttpOnly = true;

        framework.ExpireTimeSpan = TimeSpan.FromMinutes(cookie.ExpireMinutes);
        framework.SlidingExpiration = cookie.SlidingExpiration;
        framework.LoginPath = cookie.LoginPath;
        framework.AccessDeniedPath = cookie.AccessDeniedPath;

        // Renewing the cookie without renewing the token inside it would slide an envelope
        // around a credential that still died on the hour. This is what keeps the two together,
        // and it runs on every request rather than on one route, so a browser carrying a dead
        // token stops carrying it at the next thing it does.
        framework.Events.OnValidatePrincipal =
            validating => AccountSession.KeepFreshAsync(validating, mine.Value);
    });

// Kestrel refuses a body over 30 MB before any handler sees it, and a refusal there is a bare
// 413 with no sentence in it. The validator answers with the size and the format, so it has to
// be the one that refuses: Kestrel is lifted above it rather than left underneath.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Limits.MaxRequestBodySize = ImportCharacterValidator.MaxPayload + (1024 * 1024));

var app = builder.Build();

// Hashed now rather than on the first sign-in that needs it, because the request that paid for
// it would be measurably slower than its neighbours and that is the signal the decoy exists to
// remove. See SignInDecoy.
SignInDecoy.For(app.Services.GetRequiredService<IPasswordHasher>());

// Said through the configured logging pipeline rather than to the console from before Build(),
// so it lands wherever the operator sends warnings.
if (generatedSigningKey)
{
    app.Logger.LogWarning(
        "No {Setting} is configured, so this Development run generated a random one. Every " +
        "session it signs dies with the process, so a restart signs everybody out. Set a user " +
        "secret to keep them.", signingKeyPath);
}

// A failed validator is a bad request, not a server fault. Translating it here keeps every
// handler free of HTTP concerns.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is ValidationException validation)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "The request was not valid.",
            errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
        });
        return;
    }

    // A paste that is not an export is the player's mistake and reads as one, not as a
    // fault in the server.
    if (error is PathbuilderFormatException pathbuilder)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { title = pathbuilder.Message });
        return;
    }

    // A code nobody created is a 404 that says so, rather than a campaign this request starts.
    if (error is CampaignNotFoundException missing)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { title = missing.Message });
        return;
    }

    // An empty undo stack is a state and not a fault, and it is not a 404 either: the campaign
    // is there and the request simply cannot be satisfied from where it stands.
    if (error is NothingToUndoException nothing)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { title = nothing.Message });
        return;
    }

    // Treating somebody who is still immune is a state, like an empty undo stack: the request
    // is well formed, the campaign is there, and the hour simply is not up. The sentence says
    // how much of it is left, which is the only thing the player can act on.
    if (error is StillImmuneException immune)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { title = immune.Message });
        return;
    }

    // A rule of camping said no: four activities already, or somebody has succeeded at this one
    // tonight. The same kind of answer as the immunity above, and the sentence is the rule.
    if (error is CampRuleException camping)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { title = camping.Message });
        return;
    }

    // Which creature is the whole question, so the sentence carries it.
    if (error is CombatantNotFoundException absent)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { title = absent.Message });
        return;
    }

    // Taken, not invalid: the request was well formed and somebody got there first. The same
    // family as an empty undo stack, and the same status code.
    if (error is EmailAlreadyRegisteredException taken)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { title = taken.Message });
        return;
    }

    // Unauthorized rather than forbidden, and the one place in this file where the sentence is
    // deliberately uninformative: an address nobody registered and a wrong password answer
    // identically, because two sentences would turn the sign-in form into a way to ask which
    // addresses have accounts.
    if (error is SignInRefusedException refused)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { title = refused.Message });
        return;
    }

    // Forbidden rather than unauthorized: the campaign is there and this caller is not its DM.
    if (error is NotTheDmException notTheDm)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { title = notTheDm.Message });
        return;
    }

    // A body or query string the binder could not read is the caller's mistake too. Left
    // as a 500 it reads as a server fault, and the caller with a wrong field name goes
    // looking for a bug that is not there. I did exactly that against this API.
    if (error is BadHttpRequestException malformed)
    {
        context.Response.StatusCode = malformed.StatusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "The request was not valid.",
            detail = malformed.Message,
        });
        return;
    }

    if (error is JsonException json)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "The request body could not be read.",
            detail = json.Message,
        });
        return;
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { title = "Something went wrong." });
}));

await using (var scope = app.Services.CreateAsyncScope())
{
    var persistence = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var seeding = scope.ServiceProvider.GetRequiredService<IOptions<SeedingOptions>>().Value;

    if (persistence.MigrateOnStartup)
    {
        await scope.ServiceProvider.GetRequiredService<RulesDbContext>().Database.MigrateAsync();
    }

    if (seeding.Enabled)
    {
        await scope.ServiceProvider.GetRequiredService<RulesSeeder>().SeedAsync();
    }
}

// ngrok terminates TLS and forwards plain HTTP, so without this the app believes every
// request is insecure and any absolute URL it builds comes back as http on an https
// page, which a browser then blocks as mixed content.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // A tunnel is not in a known subnet, and this only ever runs behind one the operator
    // started themselves.
    KnownIPNetworks = { },
    KnownProxies = { },
});

app.UseCors();

// Serving the published client from here is a deployment convenience, not a coupling:
// there is no project reference, only a directory. The client stays a standalone
// WebAssembly app that can be hosted anywhere, which is what design/001 decided.
if (!string.IsNullOrWhiteSpace(clientRoot))
{
    if (!Directory.Exists(Path.GetFullPath(clientRoot)))
    {
        throw new DirectoryNotFoundException(
            $"Hosting:ClientRoot is '{Path.GetFullPath(clientRoot)}', which does not exist. " +
            "Publish the client first: dotnet publish src/Pf2e.Client -o <path>");
    }

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
}

// After CORS, so a cross-origin preflight is answered before anything asks who is calling, and
// after the static files, so serving a script does not decrypt a cookie to no purpose. Before
// the routes, because that is the only place the ticket can be read from.
//
// No route is authorized by this. Every route that existed before accounts still answers a
// browser with no cookie at all, which is what the seven verifiers in tools/ui-check assume.
app.UseAuthentication();
app.UseAuthorization();

app.MapRules();
app.MapCampaigns();
app.MapAccounts();
app.MapHub<CampaignHub>(app.Services.GetRequiredService<IOptions<RealtimeOptions>>().Value.HubPath);

app.MapGet("/health", async (RulesDbContext db) => Results.Ok(new
{
    database = await db.Database.CanConnectAsync(),
    rules = await db.RuleRecords.CountAsync(),
}));

// After the API routes, so a real endpoint always wins over the client's catch-all.
if (!string.IsNullOrWhiteSpace(clientRoot))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
