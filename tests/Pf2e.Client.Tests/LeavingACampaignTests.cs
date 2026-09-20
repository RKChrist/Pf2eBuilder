using Pf2e.Client.State;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Tests;

/// <summary>
/// Leaving is the verb the whole defect turns on. Until it existed the only way out of a
/// campaign was to clear the browser's storage, which took the DM key with it, so a GM could
/// not reach a second table at all.
/// </summary>
public class LeavingACampaignTests
{
    static readonly RememberedCampaign Held =
        new("AAAAAA", "key-a", new DateTimeOffset(2026, 3, 14, 19, 30, 0, TimeSpan.Zero));

    static CampaignView Table(string code) => new(
        code,
        "Exploration",
        "Dm",
        [],
        [],
        null,
        0,
        1,
        new CampSiteView(
            "PrepareCampsite", null, 0, 0, 0, null, true, 0, [], [], [], 0, 0, 0, 0));

    static CampaignState AtTheTable(string code) => new CampaignState
    {
        Code = code,
        DmKey = "key-a",
        Campaign = new RemoteData<CampaignView>.Loaded(Table(code)),
        Live = true,
        Remembered = [Held],
        Recalled = true,
        HitPointDrafts = new Dictionary<Guid, string> { [Guid.NewGuid()] = "12" },
    };

    [Fact]
    public void LeavingPutsTheBrowserBackOnTheJoinForm()
    {
        var left = CampaignReducers.On(AtTheTable("AAAAAA"), new CampaignLeft());

        Assert.Equal(string.Empty, left.Code);
        Assert.Null(left.DmKey);
        Assert.IsType<RemoteData<CampaignView>.NotAsked>(left.Campaign);
    }

    [Fact]
    public void LeavingForgetsNothingTheBrowserHolds()
    {
        var left = CampaignReducers.On(AtTheTable("AAAAAA"), new CampaignLeft());

        Assert.Equal("key-a", Assert.Single(left.Remembered).DmKey);
    }

    /// <summary>A number typed at one table must not be sitting in the field at the next.</summary>
    [Fact]
    public void LeavingEmptiesWhatWasTypedAtThatTable()
    {
        var left = CampaignReducers.On(AtTheTable("AAAAAA"), new CampaignLeft());

        Assert.Empty(left.HitPointDrafts);
    }

    /// <summary>One connection carries several tables over a session, and the push that beats
    /// the leave out of the door would otherwise reopen the campaign on the join form.</summary>
    [Fact]
    public void AChangeAtACampaignThisBrowserIsNotInIsDropped()
    {
        var left = CampaignReducers.On(AtTheTable("AAAAAA"), new CampaignLeft());

        var after = CampaignReducers.On(left, new CampaignRefreshed(Table("AAAAAA")));

        Assert.IsType<RemoteData<CampaignView>.NotAsked>(after.Campaign);
    }

    [Fact]
    public void AChangeAtTheCampaignThisBrowserIsInStillArrives()
    {
        var at = AtTheTable("AAAAAA");

        var after = CampaignReducers.On(at, new CampaignRefreshed(Table("AAAAAA") with { Mode = "Encounter" }));

        var loaded = Assert.IsType<RemoteData<CampaignView>.Loaded>(after.Campaign);
        Assert.Equal("Encounter", loaded.Value.Mode);
    }

    /// <summary>Asked once for the page load. Asking again on the next screen would walk
    /// somebody who had just left straight back into the campaign they left.</summary>
    [Fact]
    public void TheRecallHappensOncePerLoadAndLeavingDoesNotArmItAgain()
    {
        var asked = CampaignReducers.On(new CampaignState(), new CampaignRecalled());

        Assert.True(asked.Recalled);
        Assert.True(CampaignReducers.On(asked, new CampaignLeft()).Recalled);
    }
}
