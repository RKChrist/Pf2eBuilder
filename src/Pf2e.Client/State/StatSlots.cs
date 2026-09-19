using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

public enum StatSlot
{
    ArmorClass,
    Fortitude,
    Reflex,
    Will,
    Perception,
    ClassDc,
}

public sealed record StatLine(
    StatSlot Slot, string Name, string Short, Func<CharacterSheetView, BreakdownSummary> Of);

/// <summary>
/// The six numbers a card shows, as one table. The card, the breakdown sheet's title and the
/// accessible name all read the same row, so a statistic cannot be named one way in one place
/// and another way in another.
/// </summary>
public static class StatSlots
{
    public static readonly IReadOnlyList<StatLine> All =
    [
        new(StatSlot.ArmorClass, "Armor Class", "AC", character => character.ArmorClass),
        new(StatSlot.Fortitude, "Fortitude", "Fort", character => character.Fortitude),
        new(StatSlot.Reflex, "Reflex", "Ref", character => character.Reflex),
        new(StatSlot.Will, "Will", "Will", character => character.Will),
        new(StatSlot.Perception, "Perception", "Perc", character => character.Perception),
        new(StatSlot.ClassDc, "Class DC", "DC", character => character.ClassDc),
    ];

    public static StatLine Of(StatSlot slot) => All.First(line => line.Slot == slot);
}
