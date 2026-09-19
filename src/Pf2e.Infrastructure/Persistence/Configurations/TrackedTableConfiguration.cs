using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class TrackedTableConfiguration : IEntityTypeConfiguration<TrackedTable>
{
    public void Configure(EntityTypeBuilder<TrackedTable> tables)
    {
        tables.ToTable("TrackedTables");
        tables.HasKey(t => t.Id);

        // Every id in this schema is made up here or by the client, never by the store. EF reads
        // a set key on an untracked entity as proof its row already exists, so without this it
        // writes an UPDATE of a row that is not there instead of an INSERT.
        tables.Property(t => t.Id).ValueGeneratedNever();

        tables.Property(t => t.Code).HasMaxLength(12).IsRequired();

        // The code is the whole address of a table, so two of them would be two parties that
        // silently share their characters.
        tables.HasIndex(t => t.Code).IsUnique();

        tables.HasMany(t => t.Characters)
              .WithOne()
              .HasForeignKey(c => c.TableId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
