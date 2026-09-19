using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class EffectApplicationConfiguration : IEntityTypeConfiguration<EffectApplication>
{
    // Enum members are stored by name, because a reordered enum must not silently reinterpret
    // rows that were written before the reorder.
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<EffectApplication> applications)
    {
        applications.ToTable("EffectApplications");
        applications.HasKey(e => e.Id);
        applications.Property(e => e.Id).ValueGeneratedNever();

        applications.Property(e => e.Name).HasMaxLength(128).IsRequired();
        applications.Property(e => e.SourceKind).HasMaxLength(16).IsRequired();
        applications.Property(e => e.SourceKey).HasMaxLength(64);
        applications.Property(e => e.Duration).HasMaxLength(64);
        applications.Property(e => e.Timing).HasConversion<string>().HasMaxLength(24).IsRequired();
        applications.Property(e => e.PersistentDamageType).HasMaxLength(32);

        applications.HasIndex(e => e.CampaignId);

        // A handful of modifiers per effect, read only by loading the effect. A child table
        // would add an entity and a join to every read of a party to normalise data nothing
        // ever queries across.
        applications.Property(e => e.Modifiers)
                    .HasConversion(
                        modifiers => Serialize(modifiers),
                        json => Deserialize(json),
                        // A record's equality over an ImmutableArray is by reference, so
                        // comparing the stored form is what makes EF see a real change and only
                        // a real change.
                        new ValueComparer<List<EffectModifier>>(
                            (a, b) => Serialize(a) == Serialize(b),
                            v => Serialize(v).GetHashCode(StringComparison.Ordinal),
                            v => Deserialize(Serialize(v))))
                    .IsRequired();

        // Targets are a real child table rather than a JSON column, because "every effect on
        // this creature" is a query and because each target carries its own countdown.
        applications.HasMany(e => e.Targets)
                    .WithOne()
                    .HasForeignKey(t => t.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);
    }

    static string Serialize(List<EffectModifier>? modifiers) =>
        JsonSerializer.Serialize(modifiers ?? [], Json);

    static List<EffectModifier> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<EffectModifier>>(json, Json) ?? [];
}

public sealed class EffectTargetConfiguration : IEntityTypeConfiguration<EffectTarget>
{
    public void Configure(EntityTypeBuilder<EffectTarget> targets)
    {
        targets.ToTable("EffectTargets");

        // The key is the pair, because one application reaches a creature once. Sending the
        // same apply twice therefore converges rather than stacking a duplicate, which is the
        // same guarantee the client-named application id already gives.
        targets.HasKey(t => new { t.ApplicationId, t.TargetId });

        targets.Property(t => t.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        targets.HasIndex(t => t.TargetId);
    }
}
