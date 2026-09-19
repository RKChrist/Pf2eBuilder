namespace Pf2e.Domain;

/// <summary>
/// One thing a party does when they stop walking. <see cref="Minutes"/> is what it costs, which
/// is the number the camp panel tallies so "how long have we been here?" has an answer.
/// </summary>
public sealed record CampActivity(string Key, string Name, int Minutes, string What);

/// <summary>
/// The ten-minute activities.
/// <para>None of them changes a number here, and that is deliberate. Treat Wounds heals 2d8 on a
/// success and 4d8 on a critical one, which is a roll, and this app does not roll for a table
/// that has dice in front of it. What it does instead is the part people forget: the target is
/// temporarily immune for an hour afterwards, and an hour is exactly the kind of thing four
/// adults lose track of.</para>
/// </summary>
public static class CampActivities
{
    public const int TreatWoundsImmunityMinutes = 60;

    public static CampActivity TreatWounds { get; } = new(
        "treat-wounds",
        "Treat Wounds",
        10,
        "Medicine against DC 15. 2d8 healed on a success, 4d8 on a critical success, 1d8 lost on "
        + "a critical failure. The target is then immune for an hour.");

    public static CampActivity Refocus { get; } = new(
        "refocus",
        "Refocus",
        10,
        "Gets one Focus Point back, by doing whatever this character's focus spells come from.");

    public static CampActivity Repair { get; } = new(
        "repair",
        "Repair",
        10,
        "Crafting against the item's DC, with a repair kit. Mends a broken shield or a dented suit.");

    public static CampActivity IdentifyMagic { get; } = new(
        "identify-magic",
        "Identify Magic",
        10,
        "The skill that matches the tradition, against the item's DC. Says what the thing does.");

    public static IReadOnlyList<CampActivity> All { get; } =
        [TreatWounds, Refocus, Repair, IdentifyMagic];

    public static CampActivity? Find(string? key) =>
        key is null
            ? null
            : All.FirstOrDefault(activity =>
                string.Equals(activity.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A full night. Unlike the ten-minute activities this one is entirely deterministic, so the
/// engine does it rather than reminding somebody to.
/// </summary>
public static class NightsRest
{
    public const int Minutes = 8 * 60;

    /// <summary>
    /// Constitution modifier times level, and at least one per level, so a character with a
    /// Constitution penalty still wakes up better off than they went to sleep.
    /// </summary>
    public static int Recovery(int level, int constitutionModifier) =>
        Math.Max(1, constitutionModifier) * Math.Max(1, level);

    /// <summary>
    /// What a night does to one condition, by its registry key. Null means it is gone.
    /// <para>Fatigued ends. Drained and doomed each step down by one and end when they reach
    /// zero. Everything else is still there in the morning, which is the point of this being a
    /// lookup rather than a guess: sleeping does not cure being frightened of the thing that is
    /// still outside the tent.</para>
    /// </summary>
    public static int? After(string? key, int value) => key?.ToLowerInvariant() switch
    {
        "fatigued" => null,
        "drained" or "doomed" => value <= 1 ? null : value - 1,
        _ => value,
    };
}
