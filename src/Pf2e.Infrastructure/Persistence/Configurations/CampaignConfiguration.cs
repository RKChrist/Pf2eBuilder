using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
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

        campaigns.HasMany(t => t.Characters)
              .WithOne()
              .HasForeignKey(c => c.CampaignId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
