using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>Something the DM has to do that the app will not do for them.</summary>
public sealed record TurnReminder(Guid CreatureId, string Text);

/// <summary>One turn ending and the next beginning, in creature ids.</summary>
public readonly record struct TurnTick(Guid? Ending, Guid Starting, int Round);

/// <summary>
/// The duration clock from the design document, as a function over the effects in play.
/// <para>"Next turn runs the clock once for the creature whose turn ends and once for the
/// creature whose turn starts", so one tick carries both.</para>
/// </summary>
public static class TurnClock
{
    /// <summary>The seeded key the end-of-turn decrement is written against.</summary>
    public const string FrightenedKey = "frightened";

    /// <summary>
    /// Whether this tick counts one round off this effect on this creature.
    /// <para>A duration in rounds counts down at the start of the source's turn, so a one-round
    /// effect ends at the start of the caster's next turn. "Until the start or the end of your
    /// next turn" is tied to the affected creature instead. An effect in rounds whose source
    /// nobody recorded falls back to the affected creature's own turn, because a countdown with
    /// nothing to count against would never expire at all.</para>
    /// </summary>
    public static bool CountsDown(DurationTiming timing, Guid? sourceId, Guid targetId, TurnTick tick) =>
        timing switch
        {
            DurationTiming.None => false,
            DurationTiming.SourceTurnStart => tick.Starting == (sourceId ?? targetId),
            DurationTiming.TargetTurnStart => tick.Starting == targetId,
            DurationTiming.TargetTurnEnd => tick.Ending == targetId,
            _ => throw new ArgumentOutOfRangeException(nameof(timing), timing, "Not a duration timing."),
        };

    /// <summary>Frightened drops by 1 at the end of the affected creature's turn, which is a
    /// different moment from every other countdown and is the one tables forget.</summary>
    public static bool FrightenedDrops(string? sourceKey, Guid targetId, TurnTick tick) =>
        tick.Ending == targetId
        && string.Equals(sourceKey, FrightenedKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs the clock over everything in play and reports what the DM still has to do. Targets
    /// whose countdown reaches zero are removed, and an application whose last target leaves
    /// goes with it, because an effect that reached nobody is not an effect.
    /// <para>Persistent damage is reported and never applied. The document has it dealt at the
    /// end of the affected creature's turn and then ended by a DC 15 flat check, and an app that
    /// takes the hit points without rolling the check has decided half a rule.</para>
    /// </summary>
    public static ImmutableArray<TurnReminder> Run(
        TurnTick tick,
        List<EffectApplication> applications,
        Func<Guid, string> nameOf)
    {
        var reminders = ImmutableArray.CreateBuilder<TurnReminder>();

        foreach (var application in applications)
        {
            foreach (var target in application.Targets.ToList())
            {
                if (application.PersistentDamage is { } damage
                    && tick.Ending == target.TargetId)
                {
                    var type = application.PersistentDamageType is { Length: > 0 } named ? $" {named}" : string.Empty;
                    reminders.Add(new TurnReminder(
                        target.TargetId,
                        $"{nameOf(target.TargetId)} takes {damage}{type} persistent damage, then a DC 15 flat check."));
                }

                if (FrightenedDrops(application.SourceKey, target.TargetId, tick))
                {
                    target.Value -= 1;
                    if (target.Value <= 0)
                    {
                        application.Targets.Remove(target);
                        continue;
                    }
                }

                if (!CountsDown(application.Timing, application.SourceCreatureId, target.TargetId, tick)
                    || target.RemainingRounds is not { } rounds)
                {
                    continue;
                }

                target.RemainingRounds = rounds - 1;
                if (target.RemainingRounds <= 0)
                {
                    application.Targets.Remove(target);
                }
            }
        }

        applications.RemoveAll(application => application.Targets.Count == 0);
        return reminders.ToImmutable();
    }
}
