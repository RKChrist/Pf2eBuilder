using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>What an effect was put on. A character exists outside an encounter; a monster only
/// exists inside one, as a combatant.</summary>
public enum EffectTargetKind
{
    Character,
    Monster,
}

/// <summary>
/// Which of the ruleset's duration rules an effect follows. The document's duration clock is
/// four rules and this is those four, so "when does this tick" is a lookup rather than a
/// condition somebody has to get right at each call site.
/// </summary>
public enum DurationTiming
{
    /// <summary>No clock. A label a human reads and a human clears.</summary>
    None,

    /// <summary>A duration in rounds, counted down at the start of the source's turn, so a
    /// one-round effect ends at the start of the caster's next turn.</summary>
    SourceTurnStart,

    /// <summary>"Until the start of your next turn", tied to the affected creature rather than
    /// to whoever caused it. Shield and Raise a Shield are this.</summary>
    TargetTurnStart,

    /// <summary>"Until the end of your next turn", tied to the affected creature.</summary>
    TargetTurnEnd,
}

/// <summary>
/// One application of one effect, whatever it reached. Rallying Anthem on the whole party is
/// this row with five targets, and a single-target effect is this row with one.
/// <para>There is deliberately no second table for the multi-target case. Two tables would be
/// two paths into <see cref="Stacking.Resolve"/>, and design/005 says by name that the second
/// one is where the stacking rule gets broken.</para>
/// <para>The application holds what was applied. <see cref="EffectTarget"/> holds its state on
/// one creature, because the clock is per creature: frightened drops at the end of each
/// affected creature's own turn, so one party-wide frightened 2 is five countdowns.</para>
/// </summary>
public sealed class EffectApplication
{
    public required Guid Id { get; init; }
    public required Guid CampaignId { get; init; }

    public string Name { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string? SourceKey { get; set; }
    public List<EffectModifier> Modifiers { get; set; } = [];

    /// <summary>The label the screen shows, in the words whoever applied it used.</summary>
    public string? Duration { get; set; }

    public DurationTiming Timing { get; set; }

    /// <summary>The creature whose turn <see cref="DurationTiming.SourceTurnStart"/> counts
    /// down on. Null outside an encounter, and for an effect nobody claimed.</summary>
    public Guid? SourceCreatureId { get; set; }

    /// <summary>Dealt at the end of the affected creature's turn, then a DC 15 flat check ends
    /// it. Nothing here applies it: the turn clock raises a reminder and the DM rolls.</summary>
    public int? PersistentDamage { get; set; }

    public string? PersistentDamageType { get; set; }

    public List<EffectTarget> Targets { get; init; } = [];

    public EffectSource Source => EffectSource.From(SourceKind, SourceKey, [.. Modifiers]);

    /// <summary>The effect as it stands on one creature. The application's id is the effect's
    /// id, so the same buff reaching five people is one id in five sheets, which is what lets
    /// the screen show it once.</summary>
    public ActiveEffect AsActiveOn(EffectTarget target) =>
        new(Id, Name, target.Value, Source, Duration);

    public void Overwrite(ActiveEffect active, DurationTiming timing, Guid? sourceCreatureId)
    {
        Name = active.Name;
        Duration = active.Duration;
        SourceKind = active.Source.Kind;
        SourceKey = active.Source.StoredKey;
        Modifiers = [.. active.Source.StoredModifiers];
        Timing = timing;
        SourceCreatureId = sourceCreatureId;
    }
}

/// <summary>
/// One creature an application reached, and the state of the effect on that creature. The value
/// and the countdown live here rather than on the application because both move per creature.
/// </summary>
public sealed class EffectTarget
{
    public required Guid ApplicationId { get; init; }
    public required EffectTargetKind Kind { get; init; }
    public required Guid TargetId { get; init; }

    /// <summary>What a scaling effect scales by, on this creature. Frightened 2 that drops to 1
    /// at the end of this creature's turn has not changed what anybody else is carrying.</summary>
    public int Value { get; set; }

    /// <summary>Null when the effect has no clock. Zero means it has run out and the target is
    /// removed; an application whose last target is removed goes with it.</summary>
    public int? RemainingRounds { get; set; }
}

/// <summary>The one way from stored applications to the effects on one creature. Named apart
/// from the Domain.Effects registry because that one holds definitions and this one reads what
/// has actually been applied.</summary>
public static class AppliedEffects
{
    public static ImmutableArray<ActiveEffect> On(
        Guid targetId, IEnumerable<EffectApplication> applications) =>
    [
        .. applications
            .SelectMany(application => application.Targets
                .Where(target => target.TargetId == targetId)
                .Select(application.AsActiveOn)),
    ];
}
