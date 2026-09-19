namespace Pf2e.Domain.Tracking;

/// <summary>
/// What a creature adds to the d20 when initiative is rolled.
/// <para>Pure, and separate from the handler that rolls, because the die is random and a rule
/// asserted through a random number is asserted badly: twenty rolls that happen to stay in range
/// prove nothing, and the same twenty on a different day fail a correct implementation. Every
/// branch below is decided here and checked without a die.</para>
/// </summary>
public static class Initiative
{
    /// <summary>
    /// <paramref name="isAlly"/> separates the party from what they are fighting. It is what
    /// stops a scout's bonus reaching the ogre: the printed rule hands it to "you and your
    /// allies", and an ogre is neither.
    /// <para><paramref name="skillTotal"/> answers with the character's total in a named skill,
    /// or null when they have no such skill, so an activity naming one the sheet does not carry
    /// falls back to Perception rather than to zero.</para>
    /// </summary>
    public static int Modifier(
        int perception,
        ExplorationActivity? activity,
        Func<string, int?> skillTotal,
        int partyBonus,
        bool isAlly)
    {
        if (!isAlly)
        {
            return perception;
        }

        var rolled = activity is { Effect: InitiativeEffect.RolledWith, Skill: { } named }
                     && skillTotal(named) is { } total
            ? total
            : perception;

        return rolled + partyBonus;
    }

    /// <summary>The best circumstance bonus anybody's activity is handing the party, which is one
    /// number however many of them are scouting.</summary>
    public static int PartyBonus(IEnumerable<ExplorationActivity?> activities) =>
        activities
            .Where(activity => activity is { Effect: InitiativeEffect.HelpsEveryone })
            .Select(activity => activity!.Bonus)
            .DefaultIfEmpty(0)
            .Max();
}
