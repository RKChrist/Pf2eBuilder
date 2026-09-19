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
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<TrackedCharacter> Characters => Set<TrackedCharacter>();
    public DbSet<EffectApplication> EffectApplications => Set<EffectApplication>();
    public DbSet<EffectTarget> EffectTargets => Set<EffectTarget>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
