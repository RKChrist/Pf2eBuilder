using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain;
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
        // this, adding a character to a campaign it is already tracking is written as an UPDATE of
        // a row that is not there.
        characters.Property(c => c.Id).ValueGeneratedNever();

        characters.Property(c => c.Name).HasMaxLength(128).IsRequired();
        characters.Property(c => c.ClassName).HasMaxLength(128).IsRequired();
        characters.Property(c => c.AncestryName).HasMaxLength(128).IsRequired();
        characters.Property(c => c.ArmorName).HasMaxLength(128).IsRequired();

        characters.HasIndex(c => c.CampaignId);

        characters.HasMany(c => c.Effects)
                  .WithOne()
                  .HasForeignKey(e => e.CharacterId)
                  .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TrackedEffectConfiguration : IEntityTypeConfiguration<TrackedEffect>
{
    // Enum members are stored by name, because a reordered enum must not silently reinterpret
    // rows that were written before the reorder.
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<TrackedEffect> effects)
    {
        effects.ToTable("TrackedEffects");
        effects.HasKey(e => e.Id);
        effects.Property(e => e.Id).ValueGeneratedNever();
        effects.Property(e => e.Name).HasMaxLength(128).IsRequired();
        effects.Property(e => e.Duration).HasMaxLength(64);
        effects.Property(e => e.SourceKind).HasMaxLength(16).IsRequired();
        effects.Property(e => e.SourceKey).HasMaxLength(64);

        effects.HasIndex(e => e.CharacterId);

        // A handful of modifiers per effect, read only by loading the effect. A child table
        // would add an entity and a join to every read of a party to normalise data nothing
        // ever queries across.
        effects.Property(e => e.Modifiers)
               .HasConversion(
                   modifiers => Serialize(modifiers),
                   json => Deserialize(json),
                   // A record's equality over an ImmutableArray is by reference, so comparing
                   // the stored form is what makes EF see a real change and only a real change.
                   new ValueComparer<List<EffectModifier>>(
                       (a, b) => Serialize(a) == Serialize(b),
                       v => Serialize(v).GetHashCode(StringComparison.Ordinal),
                       v => Deserialize(Serialize(v))))
               .IsRequired();
    }

    static string Serialize(List<EffectModifier>? modifiers) =>
        JsonSerializer.Serialize(modifiers ?? [], Json);

    static List<EffectModifier> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<EffectModifier>>(json, Json) ?? [];
}
