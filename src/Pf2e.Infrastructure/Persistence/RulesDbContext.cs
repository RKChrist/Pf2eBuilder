using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Domain.Rules;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options)
    : DbContext(options), IRulesDbContext, ITrackerDbContext
{
    public DbSet<RuleRecord> RuleRecords => Set<RuleRecord>();
    public DbSet<SeedState> SeedState => Set<SeedState>();
    public DbSet<TrackedTable> Tables => Set<TrackedTable>();
    public DbSet<TrackedCharacter> Characters => Set<TrackedCharacter>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
