namespace Pf2e.Domain;

public enum ProficiencyRank
{
    Untrained = 0,
    Trained = 2,
    Expert = 4,
    Master = 6,
    Legendary = 8,
}

/// <summary>Variant rule flags. The default value is the standard rules.</summary>
public readonly record struct RuleOptions(bool ProficiencyWithoutLevel = false)
{
    public static RuleOptions Standard => default;
}

public static class Proficiency
{
    public static int Bonus(ProficiencyRank rank, int level, RuleOptions options = default)
    {
        if (options.ProficiencyWithoutLevel)
        {
            // GM Core's variant drops level and turns untrained from +0 into a penalty.
            // The -2 is from the design brief and is not yet verified against GM Core.
            return rank == ProficiencyRank.Untrained ? -2 : (int)rank;
        }

        return rank == ProficiencyRank.Untrained ? 0 : level + (int)rank;
    }
}
