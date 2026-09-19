using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pf2e.Api.Configuration;
using Pf2e.Api.Endpoints;
using Pf2e.Application;
using Pf2e.Infrastructure;
using Pf2e.Infrastructure.Configuration;
using Pf2e.Infrastructure.Persistence;
using AspNetCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

builder.Services.AddSection<CorsOptions>(builder.Configuration, CorsOptions.Section);
builder.Services.AddCors();

// Built from our own bound options rather than from configuration directly, so the
// post-configure binding that lets appsettings beat a library default still applies.
builder.Services.AddOptions<AspNetCorsOptions>()
    .Configure<IOptions<CorsOptions>>((aspnet, mine) => aspnet.AddDefaultPolicy(policy =>
        policy.WithOrigins([.. mine.Value.AllowedOrigins]).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

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

    // A query string that does not bind, such as Page=abc, is the caller's mistake too.
    if (error is BadHttpRequestException malformed)
    {
        context.Response.StatusCode = malformed.StatusCode;
        await context.Response.WriteAsJsonAsync(new { title = "The request was not valid.", detail = malformed.Message });
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

app.UseCors();

app.MapRules();

app.MapGet("/health", async (RulesDbContext db) => Results.Ok(new
{
    database = await db.Database.CanConnectAsync(),
    rules = await db.RuleRecords.CountAsync(),
}));

app.Run();

public partial class Program;
