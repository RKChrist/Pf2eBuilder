using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class TrackedCharacterConfiguration : IEntityTypeConfiguration<TrackedCharacter>
{
    public void Configure(EntityTypeBuilder<TrackedCharacter> characters)
    {
        characters.ToTable("TrackedCharacters");
        characters.HasKey(c => c.Id);

        // Every id in this schema is made up by a handler or by the client, never by the store.
        // EF reads a set key on an untracked entity as proof its row already exists, so without
        // this, adding a character to a campaign it is already tracking is written as an UPDATE
        // of a row that is not there.
        characters.Property(c => c.Id).ValueGeneratedNever();

        characters.Property(c => c.Name).HasMaxLength(128).IsRequired();
        characters.Property(c => c.ClassName).HasMaxLength(128).IsRequired();
        characters.Property(c => c.AncestryName).HasMaxLength(128).IsRequired();
        characters.Property(c => c.ArmorName).HasMaxLength(128).IsRequired();

        characters.HasIndex(c => c.CampaignId);
    }
}
