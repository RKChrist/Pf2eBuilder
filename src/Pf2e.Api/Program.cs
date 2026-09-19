using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pf2e.Api.Endpoints;
using Pf2e.Application;
using Pf2e.Infrastructure;
using Pf2e.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

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

app.MapRules();

app.MapGet("/health", async (RulesDbContext db) => Results.Ok(new
{
    database = await db.Database.CanConnectAsync(),
    rules = await db.RuleRecords.CountAsync(),
}));

app.Run();

public partial class Program;
