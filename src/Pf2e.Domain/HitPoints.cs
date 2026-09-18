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
}
