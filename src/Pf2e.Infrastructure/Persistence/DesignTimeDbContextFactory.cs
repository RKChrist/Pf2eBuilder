using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pf2e.Infrastructure.Persistence;

/// <summary>
/// Exists so `dotnet ef` never executes Program.cs, which migrates and seeds on startup.
/// Without this, generating a migration would try to seed against a database that does not
/// exist yet. The connection string here is only used to pick the provider's SQL dialect.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RulesDbContext>
{
    public RulesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RulesDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
