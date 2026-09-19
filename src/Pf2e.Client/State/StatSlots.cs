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
    SpellAttack,
    SpellDc,
}

/// <summary><see cref="IsRoll"/> is what decides whether the number is written with a sign. A
/// save is added to a die and reads +11; an armour class is a difficulty and reads 25.</summary>
public sealed record StatLine(
    StatSlot Slot, string Name, string Short, bool IsRoll, Func<CharacterSheetView, BreakdownSummary?> Of);

/// <summary>
/// The fixed numbers a card shows, as one table. The card, the breakdown sheet's title and the
/// accessible name all read the same row, so a statistic cannot be named one way in one place
/// and another way in another.
/// </summary>
public static class StatSlots
{
    public static readonly IReadOnlyList<StatLine> All =
    [
        new(StatSlot.ArmorClass, "Armor Class", "AC", false, character => character.ArmorClass),
        new(StatSlot.Fortitude, "Fortitude", "Fort", true, character => character.Fortitude),
        new(StatSlot.Reflex, "Reflex", "Ref", true, character => character.Reflex),
        new(StatSlot.Will, "Will", "Will", true, character => character.Will),
        new(StatSlot.Perception, "Perception", "Perc", true, character => character.Perception),
        new(StatSlot.ClassDc, "Class DC", "DC", false, character => character.ClassDc),
        new(StatSlot.SpellAttack, "Spell Attack", "Spell", true, character => character.SpellAttack),
        new(StatSlot.SpellDc, "Spell DC", "Sp DC", false, character => character.SpellDc),
    ];

    /// <summary>The slots this character actually has. A fighter has no spell attack, and a box
    /// reading zero says something false where no box says nothing.</summary>
    public static IEnumerable<StatLine> For(CharacterSheetView character) =>
        All.Where(line => line.Of(character) is not null);

    public static StatLine Of(StatSlot slot) => All.First(line => line.Slot == slot);
}

/// <summary>
/// Which number a breakdown explains. A slot is one of the fixed rows above; a skill or an attack
/// is named, because a character has as many of those as they wrote down and an enum cannot hold
/// a list that arrives from an import.
/// </summary>
public sealed record StatAddress(StatSlot? Slot = null, string? Skill = null, string? Attack = null)
{
    public static StatAddress Fixed(StatSlot slot) => new(Slot: slot);

    public static StatAddress Skilled(string name) => new(Skill: name);

    public static StatAddress Weapon(string name) => new(Attack: name);

    public string Label =>
        Slot is { } slot ? StatSlots.Of(slot).Name
        : Skill ?? Attack ?? "Breakdown";

    /// <summary>Null when the character no longer has that number, which happens when a re-import
    /// drops a weapon while its breakdown is open.</summary>
    public BreakdownSummary? In(CharacterSheetView character) =>
        Slot is { } slot ? StatSlots.Of(slot).Of(character)
        : Skill is { } skill ? character.Skills.FirstOrDefault(s => s.Name == skill)?.Value
        : Attack is { } attack ? character.Attacks.FirstOrDefault(a => a.Name == attack)?.Value
        : null;
}
