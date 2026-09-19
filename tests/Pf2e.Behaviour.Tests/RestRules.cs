using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// What a night restores, and what it does not. Pure arithmetic and a lookup, which is exactly
/// why the engine does these and reminds nobody: there is nothing to roll.
/// </summary>
public class RestRules
{
    [Theory]
    [InlineData(7, 2, 14)]
    [InlineData(1, 4, 4)]
    [InlineData(20, 5, 100)]
    public void ANightRestoresConstitutionTimesLevel(int level, int constitution, int expected)
    {
        Assert.Equal(expected, NightsRest.Recovery(level, constitution));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void AndAtLeastOnePerLevelForSomebodyWithNoConstitutionToSpeakOf(int constitution)
    {
        // The printed rule says "minimum 1", so a wizard with a Constitution penalty still wakes
        // up better off. Zero would mean a night's sleep did nothing, which no edition has said.
        Assert.Equal(7, NightsRest.Recovery(7, constitution));
    }

    [Fact]
    public void FatiguedEndsWithTheNight()
    {
        Assert.Null(NightsRest.After("fatigued", 0));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    public void DrainedStepsDownByOne(int before, int after)
    {
        Assert.Equal(after, NightsRest.After("drained", before));
    }

    [Fact]
    public void AndEndsWhenItReachesZero()
    {
        Assert.Null(NightsRest.After("drained", 1));
        Assert.Null(NightsRest.After("doomed", 1));
    }

    [Fact]
    public void SleepingDoesNotCureBeingFrightenedOfWhatIsOutsideTheTent()
    {
        // The whole point of this being a lookup rather than "clear everything": a night's rest
        // is not a heal-all, and an app that treated it as one would quietly delete the DM's
        // curse halfway through an adventure.
        Assert.Equal(2, NightsRest.After("frightened", 2));
        Assert.Equal(1, NightsRest.After("clumsy", 1));
        Assert.Equal(3, NightsRest.After("stupefied", 3));
        Assert.Equal(0, NightsRest.After("off-guard", 0));

        // Including an effect the registry has never heard of, such as one somebody typed.
        Assert.Equal(1, NightsRest.After(null, 1));
        Assert.Equal(1, NightsRest.After("the-gms-own-curse", 1));
    }

    [Fact]
    public void EveryCampActivityCostsTimeAndSaysWhatItIs()
    {
        Assert.Equal(4, CampActivities.All.Count);
        Assert.All(CampActivities.All, activity =>
        {
            Assert.Equal(10, activity.Minutes);
            Assert.False(string.IsNullOrWhiteSpace(activity.Name));
            Assert.True(activity.What.Length > 30, activity.Name);
        });

        Assert.Equal(60, CampActivities.TreatWoundsImmunityMinutes);
        Assert.NotNull(CampActivities.Find("treat-wounds"));
        Assert.Null(CampActivities.Find("second-breakfast"));
    }
}
