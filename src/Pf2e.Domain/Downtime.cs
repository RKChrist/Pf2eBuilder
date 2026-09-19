namespace Pf2e.Domain;

/// <summary>
/// One thing a character spends a day on. <see cref="Skill"/> is the one it is rolled with, and
/// null where the activity has no single skill.
/// </summary>
public sealed record DowntimeActivity(string Key, string Name, string? Skill, string What);

/// <summary>
/// The level-based DCs, which is the table every downtime activity is rolled against.
/// <para>Written as a formula, then pinned to the printed table entry by entry in the tests, so
/// the numbers are the book's and the formula is only how they are stored. That order matters:
/// a formula that drifted from the table would be a wrong DC on every roll of a downtime day,
/// and nobody would notice because the app would be confidently consistent.</para>
/// </summary>
public static class LevelBasedDc
{
    public const int Lowest = 0;
    public const int Highest = 25;

    /// <summary>
    /// Fourteen at level zero, then one per level with an extra every third level, which holds
    /// to level twenty. Past twenty the table steps by two.
    /// </summary>
    public static int For(int taskLevel)
    {
        var level = Math.Clamp(taskLevel, Lowest, Highest);

        return level <= 20
            ? 14 + level + (level / 3)
            : 40 + ((level - 20) * 2);
    }
}

/// <summary>
/// The activities a day can be spent on.
/// <para>What each one pays or produces is not here. Earn Income's table is a currency per day
/// for each task level, each proficiency rank and each degree of success, and this project
/// imports no rule text at all, so reproducing it would mean typing several hundred numbers out
/// of a book from memory. A wrong payment every downtime day is worse than a screen that gives
/// the DC and lets somebody read the one row they need.</para>
/// </summary>
public static class DowntimeActivities
{
    public static DowntimeActivity EarnIncome { get; } = new(
        "earn-income",
        "Earn Income",
        null,
        "A task of a level the GM sets, rolled with the skill or Lore it calls for. Pays by task "
        + "level and proficiency; the payment table is in the book.");

    public static DowntimeActivity Craft { get; } = new(
        "craft",
        "Craft",
        "Crafting",
        "Four days for a common item of the character's level or lower, with half its price in "
        + "materials up front, then the rest paid or worked off.");

    public static DowntimeActivity Retrain { get; } = new(
        "retrain",
        "Retrain",
        null,
        "Swaps a feat, a skill increase or a class choice for another it qualified for. Weeks, "
        + "not days, and it needs somebody to teach it.");

    public static DowntimeActivity Subsist { get; } = new(
        "subsist",
        "Subsist",
        "Survival",
        "Finds food and shelter for the day. Survival in the wild, Society in a town.");

    public static DowntimeActivity TreatDisease { get; } = new(
        "treat-disease",
        "Treat Disease",
        "Medicine",
        "A day spent tending somebody, which gives them a bonus to their next save against it.");

    public static DowntimeActivity CreateForgery { get; } = new(
        "create-forgery",
        "Create Forgery",
        "Society",
        "A day and a handwriting sample. Whoever reads it rolls Perception against the forger's "
        + "Society DC, and only if they have reason to.");

    public static DowntimeActivity LearnASpell { get; } = new(
        "learn-a-spell",
        "Learn a Spell",
        null,
        "The tradition's skill against the spell's level DC, with materials by spell rank. Adds "
        + "it to the caster's repertoire or spellbook.");

    public static IReadOnlyList<DowntimeActivity> All { get; } =
        [EarnIncome, Craft, Retrain, Subsist, TreatDisease, CreateForgery, LearnASpell];

    public static DowntimeActivity? Find(string? key) =>
        key is null
            ? null
            : All.FirstOrDefault(activity =>
                string.Equals(activity.Key, key, StringComparison.OrdinalIgnoreCase));
}
