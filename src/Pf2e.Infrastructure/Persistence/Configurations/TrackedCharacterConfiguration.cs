using System.Collections.Immutable;
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
    // By name, so a reordered ProficiencyRank or AttributeKind cannot silently reinterpret rows
    // written before the reorder.
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

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

        // Two lists read whole, replaced whole on re-import and never queried inside, so they
        // are one JSON value each rather than two more tables and two more joins on every read
        // of a party. An ImmutableArray compares by reference, so the comparer compares the
        // stored form: that is what makes EF see a real change and only a real change.
        characters.Property(c => c.Skills)
                  .HasConversion(
                      skills => Write(skills),
                      json => Read<SkillProficiency>(json),
                      new ValueComparer<ImmutableArray<SkillProficiency>>(
                          (a, b) => Write(a) == Write(b),
                          v => Write(v).GetHashCode(StringComparison.Ordinal),
                          v => Read<SkillProficiency>(Write(v))))
                  .IsRequired();

        characters.Property(c => c.Weapons)
                  .HasConversion(
                      weapons => Write(weapons),
                      json => Read<WeaponAttack>(json),
                      new ValueComparer<ImmutableArray<WeaponAttack>>(
                          (a, b) => Write(a) == Write(b),
                          v => Write(v).GetHashCode(StringComparison.Ordinal),
                          v => Read<WeaponAttack>(Write(v))))
                  .IsRequired();

        // One value or none, so null stays null rather than becoming an empty object a reader
        // would have to distinguish from a caster with no tradition.
        characters.Property(c => c.Spellcasting)
                  .HasConversion(
                      casting => casting == null ? null : JsonSerializer.Serialize(casting.Value, Json),
                      json => string.IsNullOrEmpty(json)
                          ? null
                          : JsonSerializer.Deserialize<Spellcasting>(json, Json));

        // Effects are not here any more. One application can reach five characters and a
        // monster, so they hang off the campaign; see EffectApplicationConfiguration.
    }

    static string Write<T>(ImmutableArray<T> values) =>
        JsonSerializer.Serialize(values.IsDefault ? [] : values, Json);

    static ImmutableArray<T> Read<T>(string json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<ImmutableArray<T>>(json, Json);
}
