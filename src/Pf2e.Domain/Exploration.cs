using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>How an activity changes the initiative roll it is still in effect for.</summary>
public enum InitiativeEffect
{
    /// <summary>Nothing this engine computes. The consequence is real and is a reminder.</summary>
    None,

    /// <summary>The roll is made with a named skill instead of Perception.</summary>
    RolledWith,

    /// <summary>A circumstance bonus to every ally's roll, including the character's own.</summary>
    HelpsEveryone,
}

/// <summary>
/// One thing a character is doing while the party moves. The name and the consequence come from
/// the printed rule; <see cref="Effect"/> is the part this engine can carry into the fight.
/// </summary>
public sealed record ExplorationActivity(
    string Key,
    string Name,
    string Consequence,
    InitiativeEffect Effect = InitiativeEffect.None,
    string? Skill = null,
    int Bonus = 0);

/// <summary>
/// The activities a character can be doing between fights.
/// <para>Three of them reach the fight and the rest do not, and the difference is stated rather
/// than hidden. Avoid Notice rolls a different skill for initiative, Scout hands every ally a
/// circumstance bonus, and Defend leaves the character's shield raised, which this engine cannot
/// apply because it does not know which shield they carry, so it says so as a reminder instead.
/// The other six have consequences that are entirely the table's to play out.</para>
/// <para>As with <see cref="Buffs"/>, these numbers were written out against the printed rule
/// rather than imported, because the ingest carries no rule text at all.</para>
/// </summary>
public static class ExplorationActivities
{
    public static ExplorationActivity AvoidNotice { get; } = new(
        "avoid-notice",
        "Avoid Notice",
        "Sneaks along. Rolls Stealth for initiative instead of Perception.",
        InitiativeEffect.RolledWith,
        Skill: "Stealth");

    public static ExplorationActivity Defend { get; } = new(
        "defend",
        "Defend",
        "Moves with the shield up. Starts the fight with it already raised.");

    public static ExplorationActivity DetectMagic { get; } = new(
        "detect-magic",
        "Detect Magic",
        "Casts detect magic over and over. Finds magic the party walks past, and slows them down.");

    public static ExplorationActivity FollowTheExpert { get; } = new(
        "follow-the-expert",
        "Follow the Expert",
        "Copies an ally who is better at something, and adds their proficiency bonus to it.");

    public static ExplorationActivity Hustle { get; } = new(
        "hustle",
        "Hustle",
        "Moves at double speed, for minutes equal to twice their Constitution modifier.");

    public static ExplorationActivity Investigate { get; } = new(
        "investigate",
        "Investigate",
        "Recalls Knowledge about what they pass. Perception to notice things drops to -5.");

    public static ExplorationActivity RepeatASpell { get; } = new(
        "repeat-a-spell",
        "Repeat a Spell",
        "Sustains one spell as they go, and is not ready for much else.");

    public static ExplorationActivity Scout { get; } = new(
        "scout",
        "Scout",
        "Ranges ahead. Everyone gets a +1 circumstance bonus to initiative.",
        InitiativeEffect.HelpsEveryone,
        Bonus: 1);

    public static ExplorationActivity Search { get; } = new(
        "search",
        "Search",
        "Seeks as they travel, and finds what is hidden along the way.");

    public static ImmutableArray<ExplorationActivity> All { get; } =
    [
        AvoidNotice, Defend, DetectMagic, FollowTheExpert, Hustle,
        Investigate, RepeatASpell, Scout, Search,
    ];

    public static ExplorationActivity? Find(string? key) =>
        key is null
            ? null
            : All.FirstOrDefault(activity =>
                string.Equals(activity.Key, key, StringComparison.OrdinalIgnoreCase));
}
