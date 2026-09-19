using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>
/// One fight. A campaign has at most one at a time, which is why a combatant's id can be the
/// creature's own id rather than a third identifier to keep in step.
/// </summary>
public sealed class Encounter
{
    public required Guid Id { get; init; }
    public required Guid CampaignId { get; init; }

    /// <summary>Zero until initiative is rolled, which is also what moves the campaign into
    /// Encounter mode.</summary>
    public int Round { get; set; }

    /// <summary>
    /// The turn marker, as a combatant id and never an index. A combatant added mid-fight lands
    /// wherever its initiative puts it and moves every index after it, so an index would start
    /// naming the wrong creature the moment a reinforcement arrived.
    /// </summary>
    public Guid? CurrentCombatantId { get; set; }

    public List<Combatant> Combatants { get; init; } = [];

    /// <summary>What the last turn change left for the DM to do. Stored rather than returned
    /// once, so a phone that reconnects mid-turn still sees the persistent damage it owes.</summary>
    public List<TurnReminder> Reminders { get; set; } = [];

    public ImmutableArray<TurnEntry> Order() =>
        TurnOrder.Sort(Combatants.Select(combatant => combatant.AsTurnEntry()));

    public Combatant? Find(Guid id) => Combatants.FirstOrDefault(combatant => combatant.Id == id);

    /// <summary>The name to put in a reminder. Unknown rather than a throw, because a reminder
    /// about a creature that has just left the fight is still worth reading.</summary>
    public string NameOf(Guid creatureId, IReadOnlyList<TrackedCharacter> roster) =>
        Find(creatureId) switch
        {
            MonsterCombatant monster => monster.Name,
            PlayerCombatant => roster.FirstOrDefault(c => c.Id == creatureId)?.Name ?? "A character",
            _ => roster.FirstOrDefault(c => c.Id == creatureId)?.Name ?? "A creature",
        };
}
