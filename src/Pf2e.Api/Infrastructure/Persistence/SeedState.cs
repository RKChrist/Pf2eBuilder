using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Pf2e.Api.Infrastructure.Persistence;

/// <summary>
/// What the database already holds. The seeder compares against this and does nothing when it
/// matches, which is what makes a second run free rather than merely harmless.
/// </summary>
public sealed class SeedState
{
    public int Id { get; init; } = 1;
    public required string RulesetVersion { get; set; }
    public required int RecordCount { get; set; }
    public required DateTimeOffset SeededAtUtc { get; set; }
}

public sealed class SeedStateConfiguration : IEntityTypeConfiguration<SeedState>
{
    public void Configure(EntityTypeBuilder<SeedState> state)
    {
        state.ToTable("SeedState");
        state.HasKey(s => s.Id);
        state.Property(s => s.RulesetVersion).HasMaxLength(64).IsRequired();
    }
}
