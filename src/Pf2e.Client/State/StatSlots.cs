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

/// <summary>
/// One number on a combatant's row, with the accessor for each of the two shapes that can carry
/// it. A character's defences are on the sheet in the same payload and a monster's are on its
/// stat line; reading them through one row is what stops the initiative order printing a
/// player's numbers in one order and a monster's in another.
/// </summary>
public sealed record DefenceLine(
    StatSlot Slot,
    Func<CharacterSheetView, BreakdownSummary?> OfCharacter,
    Func<MonsterStatLineView, int> OfMonster,
    Func<MonsterNumbersNow, int> OfMonsterNow)
{
    /// <summary>The number to compare a roll against right now, which is the stat block's own
    /// until something is on the monster.</summary>
    public int Now(MonsterStatLineView monster) => monster.Now is { } now ? OfMonsterNow(now) : OfMonster(monster);

    public StatLine Line => StatSlots.Of(Slot);

    /// <summary>A difficulty reads 25 and a roll reads +11, because one is compared against and
    /// the other is added to a die.</summary>
    public string Write(int total) => Line.IsRoll ? (total >= 0 ? $"+{total}" : total.ToString()) : total.ToString();
}

/// <summary>What a fight asks about a creature, in the order a stat block prints it.</summary>
public static class Defences
{
    public static readonly IReadOnlyList<DefenceLine> All =
    [
        new(StatSlot.ArmorClass, c => c.ArmorClass, m => m.ArmorClass, n => n.ArmorClass),
        new(StatSlot.Fortitude, c => c.Fortitude, m => m.Fortitude, n => n.Fortitude),
        new(StatSlot.Reflex, c => c.Reflex, m => m.Reflex, n => n.Reflex),
        new(StatSlot.Will, c => c.Will, m => m.Will, n => n.Will),
        new(StatSlot.Perception, c => c.Perception, m => m.Perception, n => n.Perception),
    ];
}
