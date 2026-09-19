using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

public enum CombatantKind
{
    PlayerCharacter,
    Adversary,
}

/// <summary>
/// The numbers a monster brings to the fight, copied from the seeded creature record when it
/// joins. Copied rather than read back through the key, so a re-seed cannot change a monster's
/// armour class in the middle of the fight it is in.
/// </summary>
public sealed record MonsterStatBlock(
    int Level,
    int MaxHitPoints,
    int ArmorClass,
    int Fortitude,
    int Reflex,
    int Will,
    int Perception,
    ImmutableArray<string> Traits)
{
    // A default ImmutableArray is not an empty one and throws on enumeration, and this type is
    // reached by deserialization, where an absent list arrives as exactly that.
    public ImmutableArray<string> Traits { get; init; } = Traits.IsDefault ? [] : Traits;
}

/// <summary>
/// One creature in the initiative order.
/// <para><see cref="Id"/> is the creature's id and not a separate one: a player combatant's id
/// is the tracked character's id. That is what makes the turn marker, the effect target and the
/// combatant one id rather than three that have to be kept in step.</para>
/// <para>The split into two subtypes is the point. A player character's hit points live on the
/// character, where the party screen already reads them, so a player combatant has nowhere to
/// keep a second copy that could drift. A monster's hit points and stat line live here, where a
/// player projection never looks.</para>
/// </summary>
public abstract class Combatant
{
    public required Guid Id { get; init; }
    public required Guid EncounterId { get; init; }

    public int Initiative { get; set; }

    public abstract CombatantKind Kind { get; }

    public TurnEntry AsTurnEntry() => new(Id, Initiative, Kind);
}

/// <summary>A character from the campaign's roster. It carries nothing of its own, which is the
/// design: everything about it is already on <see cref="TrackedCharacter"/>.</summary>
public sealed class PlayerCombatant : Combatant
{
    public override CombatantKind Kind => CombatantKind.PlayerCharacter;
}

/// <summary>
/// One monster instance. <see cref="Revealed"/> is a boolean and not three steps: unrevealed
/// means the monster is not in a player's combatant list at all, and revealed means a player
/// sees the name, the initiative and the conditions. Hit points and the stat line are never in
/// a player's payload at either setting.
/// </summary>
public sealed class MonsterCombatant : Combatant
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The seeded creature record this was drawn from, for the DM's statblock link.</summary>
    public string RuleId { get; set; } = string.Empty;

    public required MonsterStatBlock Stats { get; set; }

    public int CurrentHitPoints { get; set; }
    public int TemporaryHitPoints { get; set; }

    public bool Revealed { get; set; }

    public override CombatantKind Kind => CombatantKind.Adversary;
}
