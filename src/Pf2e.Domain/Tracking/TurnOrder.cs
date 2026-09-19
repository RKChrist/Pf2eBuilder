using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>One combatant, reduced to what deciding the order needs.</summary>
public readonly record struct TurnEntry(Guid Id, int Initiative, CombatantKind Kind);

/// <summary>Whose turn just ended, whose turn starts, and whether the order wrapped.</summary>
public readonly record struct TurnAdvance(Guid? Ending, Guid Starting, bool NewRound);

/// <summary>
/// The initiative order, as a function. It takes entries and returns entries, touches no
/// database and no clock, so the tie rule can be asserted directly rather than through a
/// handler.
/// </summary>
public static class TurnOrder
{
    /// <summary>
    /// Highest initiative first. A tie puts adversaries before player characters, which is the
    /// Player Core rule and is not a detail: it decides whether the ogre acts before the bard
    /// on the round they rolled the same number.
    /// </summary>
    public static ImmutableArray<TurnEntry> Sort(IEnumerable<TurnEntry> entries) =>
    [
        .. entries
            .OrderByDescending(entry => entry.Initiative)
            .ThenBy(entry => entry.Kind is CombatantKind.Adversary ? 0 : 1)
            // Last, so two adversaries on the same initiative keep one order between reads
            // instead of swapping every time the list is rebuilt.
            .ThenBy(entry => entry.Id),
    ];

    /// <summary>
    /// Where the marker goes next.
    /// <para>The marker is an id and not an index. A combatant added mid-fight lands wherever
    /// its initiative puts it, which moves every index after it; an index would then name a
    /// different creature than the one whose turn it is. This looks the current creature up by
    /// id in the order as it now stands, so an insertion cannot move the turn.</para>
    /// <para>A marker naming a combatant that is no longer there starts the order again from the
    /// top rather than throwing, because the creature it named has left the fight.</para>
    /// </summary>
    public static TurnAdvance? Advance(IReadOnlyList<TurnEntry> ordered, Guid? current)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var index = current is { } id ? IndexOf(ordered, id) : -1;

        if (index < 0)
        {
            return new TurnAdvance(null, ordered[0].Id, NewRound: false);
        }

        return index + 1 >= ordered.Count
            ? new TurnAdvance(ordered[index].Id, ordered[0].Id, NewRound: true)
            : new TurnAdvance(ordered[index].Id, ordered[index + 1].Id, NewRound: false);
    }

    static int IndexOf(IReadOnlyList<TurnEntry> ordered, Guid id)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
