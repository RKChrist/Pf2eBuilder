using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Pf2e.Api.Features.Rules;

/// <summary>
/// One record from Archives of Nethys. The fields a query filters on are columns; the other
/// ninety-odd, which are sparse and differ per category, live in <see cref="Mechanics"/>.
/// Eighteen tables of mostly-null columns would be a schema nobody could hold in their head.
/// </summary>
public sealed class RuleRecord
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required string SourceUrl { get; init; }
    public required string RulesetVersion { get; init; }
    public int? Level { get; init; }
    public string? Rarity { get; init; }
    public string? Type { get; init; }
    public string? PrimarySource { get; init; }
    public List<string> Traits { get; init; } = [];

    /// <summary>Every allow-listed field not promoted to a column, as a JSON object.</summary>
    public required string Mechanics { get; init; }
}

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

        // A trait filter runs over one category, and the largest is 6,568 rows. A join table
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
