using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    // Enums by name, like every other JSON value in this schema, so a reordered enum cannot
    // reinterpret a camp somebody saved last week.
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<Campaign> campaigns)
    {
        campaigns.ToTable("Campaigns");
        campaigns.HasKey(t => t.Id);

        // Every id in this schema is made up here or by the client, never by the store. EF reads
        // a set key on an untracked entity as proof its row already exists, so without this it
        // writes an UPDATE of a row that is not there instead of an INSERT.
        campaigns.Property(t => t.Id).ValueGeneratedNever();

        campaigns.Property(t => t.Code).HasMaxLength(12).IsRequired();

        // The code is the whole address of a campaign, so two of them would be two parties that
        // silently share their characters.
        campaigns.HasIndex(t => t.Code).IsUnique();

        campaigns.Property(t => t.DmKey).HasMaxLength(64).IsRequired();

        // Stored by name. A reordered enum must not silently reinterpret rows written before
        // the reorder, and "Encounter" in a row a human is reading says what 1 does not.
        campaigns.Property(t => t.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();

        // One value, read whole and replaced whole, and nothing queries inside it: the same case
        // as a character's skills. Rows written before this column existed read as a fresh camp.
        campaigns.Property(t => t.Camp)
                 .HasConversion(
                     camp => JsonSerializer.Serialize(camp, Json),
                     json => string.IsNullOrEmpty(json)
                         ? CampSite.Fresh
                         : JsonSerializer.Deserialize<CampSite>(json, Json) ?? CampSite.Fresh,
                     new ValueComparer<CampSite>(
                         (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                         v => JsonSerializer.Serialize(v, Json).GetHashCode(StringComparison.Ordinal),
                         v => v))
                 .IsRequired();

        campaigns.HasMany(t => t.Characters)
              .WithOne()
              .HasForeignKey(c => c.CampaignId)
              .OnDelete(DeleteBehavior.Cascade);

        campaigns.HasMany(t => t.EffectApplications)
              .WithOne()
              .HasForeignKey(e => e.CampaignId)
              .OnDelete(DeleteBehavior.Cascade);

        campaigns.HasOne(t => t.Encounter)
              .WithOne()
              .HasForeignKey<Encounter>(e => e.CampaignId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
