using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Rules;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class RuleRecordConfiguration : IEntityTypeConfiguration<RuleRecord>
{
    public void Configure(EntityTypeBuilder<RuleRecord> rules)
    {
        rules.ToTable("RuleRecords");
        rules.HasKey(r => r.Id);
        rules.Property(r => r.Id).HasMaxLength(64);
        rules.Property(r => r.Category).HasMaxLength(64).IsRequired();
        rules.Property(r => r.Name).HasMaxLength(256).IsRequired();
        rules.Property(r => r.SourceUrl).HasMaxLength(512).IsRequired();
        rules.Property(r => r.RulesetVersion).HasMaxLength(64).IsRequired();
        rules.Property(r => r.Rarity).HasMaxLength(32);
        rules.Property(r => r.Type).HasMaxLength(64);
        rules.Property(r => r.PrimarySource).HasMaxLength(256);

        // A trait filter runs over one category, and the largest is 6,469 rows. A join table
        // would add an entity and a join to every query to speed up a scan that is already
        // sub-millisecond. Revisit with a generated column and an index if profiling disagrees.
        rules.Property(r => r.Traits)
             .HasConversion(
                 traits => JsonSerializer.Serialize(traits, (JsonSerializerOptions?)null),
                 json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>(),
                 new ValueComparer<List<string>>(
                     (a, b) => a != null && b != null && a.SequenceEqual(b),
                     v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                     v => v.ToList()))
             .IsRequired();

        rules.Property(r => r.Mechanics).IsRequired();

        rules.HasIndex(r => new { r.Category, r.Level });
        rules.HasIndex(r => new { r.Category, r.Name });
    }
}

/// <summary>
/// The rename index. Keyed by the old name and the category it was in, because a renamed feat
/// must not resolve to a renamed spell, and because the pair is what an importer holds when it
/// is looking one up.
/// </summary>
public sealed class RuleAliasConfiguration : IEntityTypeConfiguration<RuleAlias>
{
    public void Configure(EntityTypeBuilder<RuleAlias> aliases)
    {
        aliases.ToTable("RuleAliases");
        aliases.HasKey(a => new { a.Was, a.Category });
        aliases.Property(a => a.Was).HasMaxLength(256);
        aliases.Property(a => a.Category).HasMaxLength(64);
        aliases.Property(a => a.NowId).HasMaxLength(64).IsRequired();
    }
}
