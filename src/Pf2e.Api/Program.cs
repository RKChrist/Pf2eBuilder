using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pf2e.Api.Infrastructure.Configuration;
using Pf2e.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSection<PersistenceOptions>(builder.Configuration, PersistenceOptions.Section);
builder.Services.AddSection<SeedingOptions>(builder.Configuration, SeedingOptions.Section);

builder.Services.AddDbContext<RulesDbContext>((services, db) =>
{
    var persistence = services.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    switch (persistence.Provider)
    {
        case "Sqlite":
            db.UseSqlite(persistence.ConnectionString);
            break;
        default:
            throw new InvalidOperationException(
                $"Database:Provider is '{persistence.Provider}', which this build does not support. " +
                "Supported providers: Sqlite.");
    }
});

builder.Services.AddScoped<RulesSeeder>();

var app = builder.Build();

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

app.MapGet("/health", async (RulesDbContext db) => Results.Ok(new
{
    database = await db.Database.CanConnectAsync(),
    rules = await db.RuleRecords.CountAsync(),
}));

app.Run();

public partial class Program;
