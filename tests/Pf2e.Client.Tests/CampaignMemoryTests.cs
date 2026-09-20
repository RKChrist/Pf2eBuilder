using System.Text.Json;
using Pf2e.Client.State;

namespace Pf2e.Client.Tests;

/// <summary>
/// A browser holds several campaigns, and the DM key it holds for each one is the campaign. The
/// slot this replaces held exactly one, so a GM who started a second table lost the first for
/// good; these say what the list does instead.
/// </summary>
public class CampaignMemoryTests
{
    static readonly DateTimeOffset Evening = new(2026, 3, 14, 19, 30, 0, TimeSpan.Zero);

    readonly BrowserStorage _storage = new();
    readonly StoppedClock _clock = new(Evening);

    CampaignMemory Memory => new(_storage, _clock);

    static string Codes(IReadOnlyList<RememberedCampaign> remembered) =>
        string.Join(' ', remembered.Select(campaign => campaign.Code));

    [Fact]
    public async Task AFreshBrowserRemembersNothing()
    {
        Assert.Empty(await Memory.ReadAllAsync());
    }

    [Fact]
    public async Task ACampaignIsRememberedWithTheKeyItWasOpenedWith()
    {
        var remembered = await Memory.RememberAsync("ABC234", "key-1");

        var only = Assert.Single(remembered);
        Assert.Equal("ABC234", only.Code);
        Assert.Equal("key-1", only.DmKey);
        Assert.Equal(Evening, only.LastOpened);
    }

    [Fact]
    public async Task ASecondCampaignDoesNotEvictTheFirst()
    {
        await Memory.RememberAsync("AAAAAA", "key-a");
        _clock.Now = Evening.AddMinutes(20);
        await Memory.RememberAsync("BBBBBB", "key-b");

        var remembered = await Memory.ReadAllAsync();

        Assert.Equal("BBBBBB AAAAAA", Codes(remembered));
        Assert.Equal("key-a", remembered.Single(campaign => campaign.Code == "AAAAAA").DmKey);
    }

    [Fact]
    public async Task OpeningOneAgainMovesItToTheHeadRatherThanListingItTwice()
    {
        await Memory.RememberAsync("AAAAAA", "key-a");
        await Memory.RememberAsync("BBBBBB", "key-b");
        _clock.Now = Evening.AddHours(2);
        var remembered = await Memory.RememberAsync("AAAAAA", "key-a");

        Assert.Equal("AAAAAA BBBBBB", Codes(remembered));
        Assert.Equal(Evening.AddHours(2), remembered[0].LastOpened);
    }

    /// <summary>Typing your own campaign's code arrives with no key, because the key is not on
    /// the screen and never was. Reading it as "this browser is a player now" would throw away
    /// the only copy of it.</summary>
    [Fact]
    public async Task OpeningACampaignWithoutAKeyKeepsTheKeyThisBrowserAlreadyHeldForIt()
    {
        await Memory.RememberAsync("ABC234", "key-1");

        var remembered = await Memory.RememberAsync("ABC234", null);

        Assert.Equal("key-1", Assert.Single(remembered).DmKey);
    }

    [Fact]
    public async Task ACampaignOpenRightNowUnderTheOldKeyBecomesTheHeadOfTheList()
    {
        _storage["pf2e.campaign"] = """{"Code":"OLDONE","DmKey":"key-old"}""";

        var remembered = await Memory.ReadAllAsync();

        var only = Assert.Single(remembered);
        Assert.Equal("OLDONE", only.Code);
        Assert.Equal("key-old", only.DmKey);
        Assert.Equal(Evening, only.LastOpened);
        Assert.Null(_storage["pf2e.campaign"]);
        Assert.Contains("OLDONE", _storage["pf2e.campaigns"]);
    }

    [Fact]
    public async Task TheCarriedOverCampaignIsCarriedOverOnce()
    {
        _storage["pf2e.campaign"] = """{"Code":"OLDONE","DmKey":"key-old"}""";
        await Memory.ReadAllAsync();

        await Memory.ForgetAsync("OLDONE");

        Assert.Empty(await Memory.ReadAllAsync());
    }

    /// <summary>A tab that was open while another wrote the list. The old key still wins the
    /// head, because it is the campaign somebody is looking at.</summary>
    [Fact]
    public async Task TheOldKeyIsFoldedInEvenWhenTheListAlreadyHasCampaigns()
    {
        await Memory.RememberAsync("BBBBBB", "key-b");
        _storage["pf2e.campaign"] = """{"Code":"OLDONE","DmKey":"key-old"}""";

        var remembered = await Memory.ReadAllAsync();

        Assert.Equal("OLDONE BBBBBB", Codes(remembered));
        Assert.Null(_storage["pf2e.campaign"]);
    }

    [Fact]
    public async Task OnlyTheEightMostRecentAreKept()
    {
        for (var opened = 1; opened <= 9; opened++)
        {
            _clock.Now = Evening.AddMinutes(opened);
            await Memory.RememberAsync($"CODE{opened:D2}", $"key-{opened}");
        }

        var remembered = await Memory.ReadAllAsync();

        Assert.Equal(8, remembered.Count);
        Assert.Equal("CODE09", remembered[0].Code);
        Assert.DoesNotContain(remembered, campaign => campaign.Code == "CODE01");
    }

    [Fact]
    public async Task ForgettingDropsThatCampaignAndItsKeyAndNothingElse()
    {
        await Memory.RememberAsync("AAAAAA", "key-a");
        await Memory.RememberAsync("BBBBBB", "key-b");

        var remembered = await Memory.ForgetAsync("BBBBBB");

        Assert.Equal("AAAAAA", Codes(remembered));
        Assert.DoesNotContain("key-b", _storage["pf2e.campaigns"]);
        Assert.Contains("key-a", _storage["pf2e.campaigns"]);
    }

    [Fact]
    public async Task ABrowserWithNoStorageAtAllStillAnswers()
    {
        _storage.Broken = true;

        Assert.Single(await Memory.RememberAsync("ABC234", "key-1"));
        Assert.Empty(await Memory.ReadAllAsync());
        Assert.Empty(await Memory.ForgetAsync("ABC234"));
    }

    [Fact]
    public async Task StorageSomebodyElseWroteIsTreatedAsNothingRemembered()
    {
        _storage["pf2e.campaigns"] = "not json at all";

        Assert.Empty(await Memory.ReadAllAsync());
    }

    [Fact]
    public async Task WhatIsStoredIsAListThatCanBeReadBack()
    {
        await Memory.RememberAsync("ABC234", "key-1");

        var stored = JsonSerializer.Deserialize<List<RememberedCampaign>>(_storage["pf2e.campaigns"]!);

        Assert.Equal("ABC234", Assert.Single(stored!).Code);
    }
}
