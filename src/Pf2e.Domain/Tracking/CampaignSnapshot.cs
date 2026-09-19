using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

public sealed record CharacterHealthSnapshot(Guid Id, int CurrentHitPoints, int TemporaryHitPoints, int HeroPoints);

public sealed record CombatantSnapshot(
    Guid Id,
    CombatantKind Kind,
    int Initiative,
    string Name,
    string RuleId,
    MonsterStatBlock? Stats,
    int CurrentHitPoints,
    int TemporaryHitPoints,
    bool Revealed);

public sealed record EffectTargetSnapshot(EffectTargetKind Kind, Guid TargetId, int Value, int? RemainingRounds);

public sealed record EffectSnapshot(
    Guid Id,
    string Name,
    string SourceKind,
    string? SourceKey,
    ImmutableArray<EffectModifier> Modifiers,
    string? Duration,
    DurationTiming Timing,
    Guid? SourceCreatureId,
    int? PersistentDamage,
    string? PersistentDamageType,
    ImmutableArray<EffectTargetSnapshot> Targets);

public sealed record EncounterSnapshot(
    Guid Id,
    int Round,
    Guid? CurrentCombatantId,
    ImmutableArray<CombatantSnapshot> Combatants,
    ImmutableArray<TurnReminder> Reminders);

/// <summary>
/// Everything about a campaign that a turn at the table changes, as one value.
/// <para>The design document describes the undo stack as "a list of prior state snapshots,
/// capped", and a prior state is the inverse of whatever produced the state after it. Writing
/// inverses per command instead would mean a new inverse for every command anybody adds, and
/// the one nobody wrote is the one that corrupts the fight.</para>
/// </summary>
public sealed record CampaignSnapshot(
    string Label,
    CampaignMode Mode,
    ImmutableArray<CharacterHealthSnapshot> Characters,
    ImmutableArray<EffectSnapshot> Effects,
    EncounterSnapshot? Encounter);

/// <summary>Taking and putting back, as functions over the campaign.</summary>
public static class CampaignSnapshots
{
    public static CampaignSnapshot Of(Campaign campaign, string label) => new(
        label,
        campaign.Mode,
        [.. campaign.Characters.Select(c =>
            new CharacterHealthSnapshot(c.Id, c.CurrentHitPoints, c.TemporaryHitPoints, c.HeroPoints))],
        [.. campaign.EffectApplications.Select(Of)],
        campaign.Encounter is { } encounter ? Of(encounter) : null);

    /// <summary>
    /// Puts the campaign back where the snapshot found it. Combatants and effects are reconciled
    /// rather than assigned, because an undo has to remove what the command added as well as put
    /// back what it changed; a restore that only wrote the fields it recognised would leave a
    /// monster somebody has just undone standing in the initiative order.
    /// </summary>
    public static void Restore(Campaign campaign, CampaignSnapshot snapshot)
    {
        campaign.Mode = snapshot.Mode;

        foreach (var health in snapshot.Characters)
        {
            if (campaign.Characters.FirstOrDefault(c => c.Id == health.Id) is not { } character)
            {
                continue;
            }

            character.CurrentHitPoints = health.CurrentHitPoints;
            character.TemporaryHitPoints = health.TemporaryHitPoints;
            character.HeroPoints = health.HeroPoints;
        }

        RestoreEffects(campaign, snapshot.Effects);
        RestoreEncounter(campaign, snapshot.Encounter);
    }

    static EffectSnapshot Of(EffectApplication application) => new(
        application.Id,
        application.Name,
        application.SourceKind,
        application.SourceKey,
        [.. application.Modifiers],
        application.Duration,
        application.Timing,
        application.SourceCreatureId,
        application.PersistentDamage,
        application.PersistentDamageType,
        [.. application.Targets.Select(t =>
            new EffectTargetSnapshot(t.Kind, t.TargetId, t.Value, t.RemainingRounds))]);

    static EncounterSnapshot Of(Encounter encounter) => new(
        encounter.Id,
        encounter.Round,
        encounter.CurrentCombatantId,
        [.. encounter.Combatants.Select(Of)],
        [.. encounter.Reminders]);

    static CombatantSnapshot Of(Combatant combatant) => combatant switch
    {
        MonsterCombatant monster => new CombatantSnapshot(
            monster.Id, monster.Kind, monster.Initiative, monster.Name, monster.RuleId,
            monster.Stats, monster.CurrentHitPoints, monster.TemporaryHitPoints, monster.Revealed),
        _ => new CombatantSnapshot(
            combatant.Id, combatant.Kind, combatant.Initiative,
            string.Empty, string.Empty, null, 0, 0, true),
    };

    static void RestoreEffects(Campaign campaign, ImmutableArray<EffectSnapshot> effects)
    {
        var wanted = effects.ToDictionary(effect => effect.Id);
        campaign.EffectApplications.RemoveAll(application => !wanted.ContainsKey(application.Id));

        foreach (var effect in effects)
        {
            var application = campaign.EffectApplications.FirstOrDefault(a => a.Id == effect.Id);
            if (application is null)
            {
                application = new EffectApplication { Id = effect.Id, CampaignId = campaign.Id };
                campaign.EffectApplications.Add(application);
            }

            application.Name = effect.Name;
            application.SourceKind = effect.SourceKind;
            application.SourceKey = effect.SourceKey;
            application.Modifiers = [.. effect.Modifiers];
            application.Duration = effect.Duration;
            application.Timing = effect.Timing;
            application.SourceCreatureId = effect.SourceCreatureId;
            application.PersistentDamage = effect.PersistentDamage;
            application.PersistentDamageType = effect.PersistentDamageType;

            var ids = effect.Targets.Select(t => t.TargetId).ToHashSet();
            application.Targets.RemoveAll(target => !ids.Contains(target.TargetId));

            foreach (var target in effect.Targets)
            {
                var row = application.Targets.FirstOrDefault(t => t.TargetId == target.TargetId);
                if (row is null)
                {
                    row = new EffectTarget
                    {
                        ApplicationId = application.Id,
                        Kind = target.Kind,
                        TargetId = target.TargetId,
                    };
                    application.Targets.Add(row);
                }

                row.Value = target.Value;
                row.RemainingRounds = target.RemainingRounds;
            }
        }
    }

    static void RestoreEncounter(Campaign campaign, EncounterSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            campaign.Encounter = null;
            return;
        }

        var encounter = campaign.Encounter;
        if (encounter is null || encounter.Id != snapshot.Id)
        {
            encounter = new Encounter { Id = snapshot.Id, CampaignId = campaign.Id };
            campaign.Encounter = encounter;
        }

        encounter.Round = snapshot.Round;
        encounter.CurrentCombatantId = snapshot.CurrentCombatantId;
        encounter.Reminders = [.. snapshot.Reminders];

        var ids = snapshot.Combatants.Select(c => c.Id).ToHashSet();
        encounter.Combatants.RemoveAll(combatant => !ids.Contains(combatant.Id));

        foreach (var wanted in snapshot.Combatants)
        {
            var combatant = encounter.Find(wanted.Id) ?? Added(encounter, wanted);
            combatant.Initiative = wanted.Initiative;

            if (combatant is MonsterCombatant monster)
            {
                monster.Name = wanted.Name;
                monster.RuleId = wanted.RuleId;
                monster.Stats = wanted.Stats ?? monster.Stats;
                monster.CurrentHitPoints = wanted.CurrentHitPoints;
                monster.TemporaryHitPoints = wanted.TemporaryHitPoints;
                monster.Revealed = wanted.Revealed;
            }
        }
    }

    static Combatant Added(Encounter encounter, CombatantSnapshot wanted)
    {
        Combatant combatant = wanted.Kind is CombatantKind.Adversary
            ? new MonsterCombatant
            {
                Id = wanted.Id,
                EncounterId = encounter.Id,
                Stats = wanted.Stats ?? new MonsterStatBlock(0, 0, 0, 0, 0, 0, 0, []),
            }
            : new PlayerCombatant { Id = wanted.Id, EncounterId = encounter.Id };

        encounter.Combatants.Add(combatant);
        return combatant;
    }
}
