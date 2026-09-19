namespace Pf2e.Domain;

public static class HitPoints
{
    public static int Max(
        int ancestryHp,
        int classHp,
        int level,
        int conModifier,
        int bonusHp = 0,
        int bonusHpPerLevel = 0) =>
        ancestryHp + bonusHp + level * (classHp + conModifier + bonusHpPerLevel);

    /// <summary>
    /// Drained costs hit points as well as imposing a penalty, which no modifier can express.
    /// The loss is the condition's value times the character's level, taken from both current
    /// and maximum hit points.
    /// </summary>
    public static int DrainedLoss(int drainedValue, int level) => drainedValue * level;
}
