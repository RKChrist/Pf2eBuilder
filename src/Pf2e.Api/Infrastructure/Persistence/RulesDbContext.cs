using Microsoft.EntityFrameworkCore;
using Pf2e.Api.Features.Rules;

namespace Pf2e.Api.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options) : DbContext(options)
{
    public DbSet<RuleRecord> RuleRecords => Set<RuleRecord>();
    public DbSet<SeedState> SeedState => Set<SeedState>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
