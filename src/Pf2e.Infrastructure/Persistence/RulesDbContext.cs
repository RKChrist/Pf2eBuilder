using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Domain.Accounts;
using Pf2e.Domain.Rules;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options)
    : DbContext(options), IRulesDbContext, ITrackerDbContext, IAccountsDbContext
{
    public DbSet<RuleRecord> RuleRecords => Set<RuleRecord>();
    public DbSet<RuleAlias> RuleAliases => Set<RuleAlias>();
    public DbSet<SeedState> SeedState => Set<SeedState>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<TrackedCharacter> Characters => Set<TrackedCharacter>();
    public DbSet<EffectApplication> EffectApplications => Set<EffectApplication>();
    public DbSet<EffectTarget> EffectTargets => Set<EffectTarget>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Combatant> Combatants => Set<Combatant>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
