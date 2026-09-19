using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// What a creature adds to the d20, with no d20 in sight. The handler that rolls is exercised
/// against a real database elsewhere; the rule is here, because a rule asserted through a random
/// number is asserted badly. Twenty rolls that stay in range prove nothing and the same twenty
/// on a different day fail a correct implementation.
/// </summary>
public class InitiativeRules
{
    static int? NoSkills(string _) => null;

    static Func<string, int?> Skills(params (string Name, int Total)[] skills) =>
        name => skills.FirstOrDefault(skill => skill.Name == name) is { Name: not null } found
            ? found.Total
            : null;

    [Fact]
    public void WithNoActivityACreatureRollsItsPerception()
    {
        Assert.Equal(12, Initiative.Modifier(12, null, NoSkills, 0, isAlly: true));
    }

    [Fact]
    public void AvoidNoticeRollsTheSkillTheActivityNames()
    {
        var stealthy = Initiative.Modifier(
            12, ExplorationActivities.AvoidNotice, Skills(("Stealth", 14)), 0, isAlly: true);

        Assert.Equal(14, stealthy);
    }

    [Fact]
    public void AndFallsBackToPerceptionWhenTheSheetCarriesNoSuchSkill()
    {
        // A monster added as an ally, or a character imported from an export with no skills in
        // it. Zero would be a number nobody's sheet says.
        Assert.Equal(12, Initiative.Modifier(
            12, ExplorationActivities.AvoidNotice, NoSkills, 0, isAlly: true));
    }

    [Fact]
    public void AScoutsBonusReachesAnAllyAndNotWhatTheyAreFighting()
    {
        Assert.Equal(13, Initiative.Modifier(12, null, NoSkills, 1, isAlly: true));

        // "You and your allies" is the printed wording, and an ogre is neither.
        Assert.Equal(12, Initiative.Modifier(12, null, NoSkills, 1, isAlly: false));
    }

    [Fact]
    public void AndStacksWithTheSkillAvoidNoticeSubstituted()
    {
        var both = Initiative.Modifier(
            12, ExplorationActivities.AvoidNotice, Skills(("Stealth", 14)), 1, isAlly: true);

        Assert.Equal(15, both);
    }

    [Fact]
    public void AnActivityWithNoInitiativeConsequenceChangesNothing()
    {
        foreach (var activity in ExplorationActivities.All.Where(a => a.Effect is InitiativeEffect.None))
        {
            Assert.Equal(12, Initiative.Modifier(
                12, activity, Skills(("Stealth", 14)), 0, isAlly: true));
        }
    }

    [Fact]
    public void ThePartyBonusIsOneNumberHoweverManyPeopleAreScouting()
    {
        Assert.Equal(0, Initiative.PartyBonus([]));
        Assert.Equal(0, Initiative.PartyBonus([null, ExplorationActivities.Search]));
        Assert.Equal(1, Initiative.PartyBonus([ExplorationActivities.Scout]));

        // Two scouts do not scout twice as well. Circumstance bonuses of the same kind do not
        // stack, and the rule that says so is the same one the sheet uses.
        Assert.Equal(1, Initiative.PartyBonus(
            [ExplorationActivities.Scout, ExplorationActivities.Scout, ExplorationActivities.AvoidNotice]));
    }
}
