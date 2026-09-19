using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> encounters)
    {
        encounters.ToTable("Encounters");
        encounters.HasKey(e => e.Id);
        encounters.Property(e => e.Id).ValueGeneratedNever();
        encounters.HasIndex(e => e.CampaignId).IsUnique();

        // A short list replaced whole on every turn change, read only by loading the encounter.
        encounters.Property(e => e.Reminders)
                  .HasConversion(
                      reminders => Serialize(reminders),
                      json => Deserialize(json),
                      new ValueComparer<List<TurnReminder>>(
                          (a, b) => Serialize(a) == Serialize(b),
                          v => Serialize(v).GetHashCode(StringComparison.Ordinal),
                          v => Deserialize(Serialize(v))))
                  .IsRequired();

        encounters.HasMany(e => e.Combatants)
                  .WithOne()
                  .HasForeignKey(c => c.EncounterId)
                  .OnDelete(DeleteBehavior.Cascade);
    }

    static string Serialize(List<TurnReminder>? reminders) =>
        JsonSerializer.Serialize(reminders ?? []);

    static List<TurnReminder> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<TurnReminder>>(json) ?? [];
}

public sealed class CombatantConfiguration : IEntityTypeConfiguration<Combatant>
{
    public void Configure(EntityTypeBuilder<Combatant> combatants)
    {
        // One table with a discriminator, because the two kinds are read together as one
        // initiative order and separating them would mean two queries and a merge on every read.
        combatants.ToTable("Combatants");
        combatants.HasKey(c => c.Id);
        combatants.Property(c => c.Id).ValueGeneratedNever();
        combatants.HasIndex(c => c.EncounterId);
        combatants.Ignore(c => c.Kind);

        // Named CombatantType and not Kind: EF maps a discriminator as a property, and Kind is
        // already a computed property on this type.
        combatants.HasDiscriminator<string>("CombatantType")
                  .HasValue<PlayerCombatant>(nameof(CombatantKind.PlayerCharacter))
                  .HasValue<MonsterCombatant>(nameof(CombatantKind.Adversary));
    }
}

public sealed class MonsterCombatantConfiguration : IEntityTypeConfiguration<MonsterCombatant>
{
    public void Configure(EntityTypeBuilder<MonsterCombatant> monsters)
    {
        monsters.Property(m => m.Name).HasMaxLength(128).IsRequired();
        monsters.Property(m => m.RuleId).HasMaxLength(64).IsRequired();

        // The stat line as one document. Nothing queries across a monster's saves, and keeping
        // it in one column is also what makes "the DM projection reads this, the player
        // projection does not" a single decision rather than seven.
        monsters.Property(m => m.Stats)
                .HasConversion(
                    stats => JsonSerializer.Serialize(stats, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<MonsterStatBlock>(json, (JsonSerializerOptions?)null)!)
                .IsRequired();
    }
}
