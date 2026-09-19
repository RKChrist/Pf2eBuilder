using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pf2e.Api.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>Builds a real database from migrations and seeds it once for the whole suite.</summary>
public sealed class SeededDatabase : IAsyncLifetime
{
    public static readonly string SeedPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/rules-import/out/seed"));

    const string Version = "aon-20260902-190924";

    string _file = string.Empty;
    public SeedingReport FirstRun { get; private set; } = null!;
    public SeedingReport SecondRun { get; private set; } = null!;

    public RulesDbContext NewContext() => Context(_file);

    static RulesDbContext Context(string file) =>
        new(new DbContextOptionsBuilder<RulesDbContext>().UseSqlite($"Data Source={file}").Options);

    public static RulesSeeder SeederFor(RulesDbContext db, string seedPath) =>
        new(db, Options.Create(new SeedingOptions
        {
            Enabled = true,
            SeedDataPath = seedPath,
            RulesetVersion = Version,
        }), NullLogger<RulesSeeder>.Instance);

    public async Task InitializeAsync()
    {
        _file = Path.Combine(Path.GetTempPath(), $"pf2e-tests-{Guid.NewGuid():N}.db");

        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = NewContext())
        {
            FirstRun = await SeederFor(db, SeedPath).SeedAsync();
        }

        await using (var db = NewContext())
        {
            SecondRun = await SeederFor(db, SeedPath).SeedAsync();
        }
    }

    public Task DisposeAsync()
    {
        SqliteConnectionPool.Clear();
        File.Delete(_file);
        return Task.CompletedTask;
    }
}

static class SqliteConnectionPool
{
    public static void Clear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}
