using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Domain.Rules;

namespace Pf2e.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options)
    : DbContext(options), IRulesDbContext
{
    public DbSet<RuleRecord> RuleRecords => Set<RuleRecord>();
    public DbSet<SeedState> SeedState => Set<SeedState>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
