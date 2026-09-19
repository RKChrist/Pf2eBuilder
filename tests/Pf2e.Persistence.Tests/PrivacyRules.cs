using System.Text.Json;
using System.Text.RegularExpressions;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The boundary design/006 calls load-bearing: a player's payload never contains a monster's
/// hit points or stat line, and never contains an unrevealed monster at all.
/// <para>These assert the absence in the serialised payload rather than in the screen. A test
/// that asserted the UI hides the number would pass while the number sat in the JSON, and
/// anything the browser receives, the browser has.</para>
/// </summary>
public partial class PrivacyRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    /// <summary>A Doppelganger, whose seeded record states exactly fifty hit points, so the
    /// number under test is the ruleset's and not one this test arranged.</summary>
    const string FiftyHitPointCreature = "creature-126";

    const string MonsterName = "Ogre Warden";

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidShaped { get; }

    /// <summary>
    /// A generated Guid is hexadecimal, so it contains "50" often enough to make an absence
    /// test over a payload flaky. Taking the ids out first is what makes the assertion about
    /// hit points rather than about luck. The campaign code cannot contribute one: its alphabet
    /// omits 0 and 1 because those are the characters people mishear.
    /// </summary>
    static string WithoutIds<T>(T payload) =>
        GuidShaped.Replace(JsonSerializer.Serialize(payload), "<id>");

    RecordingBroadcaster Broadcaster { get; } = new();

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    async Task<(CreatedCampaignView Campaign, Guid Monster)> WithOneMonster(bool revealed)
    {
        CreatedCampaignView campaign;
        await using (var db = database.NewContext())
        {
            campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        }

        await using (var db = database.NewContext())
        {
            await new ImportCharacterHandler(db, db, Broadcaster)
                .Handle(new ImportCharacter(campaign.Code, Fixture("gnibbo.json")), default);
        }

        await using (var db = database.NewContext())
        {
            await new AddCombatantHandler(db, db, Broadcaster).Handle(
                new AddCombatant(campaign.Code, campaign.DmKey, FiftyHitPointCreature, null, MonsterName),
                default);
        }

        var monster = (await Read(campaign.Code, campaign.DmKey)).Encounter!
            .Combatants.Single(c => c.Kind == "Adversary").Id;

        if (revealed)
        {
            await using var db = database.NewContext();
            await new RevealMonsterHandler(db, Broadcaster)
                .Handle(new RevealMonster(campaign.Code, campaign.DmKey, monster, true), default);
        }

        return (campaign, monster);
    }

    async Task<CampaignView> Read(string code, string? dmKey = null)
    {
        await using var db = database.NewContext();
        return await new GetCampaignHandler(db).Handle(new GetCampaign(code, dmKey), default);
    }

    // The test read first. The DM half is not decoration: without it the absence passes
    // vacuously against an empty response, a broken projection or a monster that never arrived.
    [Fact]
    public async Task APlayersPayloadDoesNotContainARevealedMonstersFiftyHitPointsAndTheDmsDoes()
    {
        var (campaign, monster) = await WithOneMonster(revealed: true);

        var forDm = WithoutIds(await Read(campaign.Code, campaign.DmKey));
        var forPlayer = WithoutIds(await Read(campaign.Code));

        Assert.Contains("50", forDm);
        Assert.DoesNotContain("50", forPlayer);

        // And the monster is genuinely in the player's payload, so the absence above is about
        // hit points and not about the monster having been dropped for some other reason.
        Assert.Contains(MonsterName, forPlayer);
        var seen = Assert.Single((await Read(campaign.Code)).Encounter!.Combatants,
                                 c => c.Kind == "Adversary");
        Assert.Equal(monster, seen.Id);
        Assert.Null(seen.Monster);
    }

    [Fact]
    public async Task APlayersPayloadDoesNotContainAnUnrevealedMonstersNameAndTheDmsDoes()
    {
        var (campaign, _) = await WithOneMonster(revealed: false);

        var forDm = WithoutIds(await Read(campaign.Code, campaign.DmKey));
        var forPlayer = WithoutIds(await Read(campaign.Code));

        Assert.Contains(MonsterName, forDm);
        Assert.DoesNotContain(MonsterName, forPlayer);

        // Dropped and not blanked. A row with an empty name still tells the players something
        // is standing there, which is the thing the DM was keeping from them.
        Assert.DoesNotContain((await Read(campaign.Code)).Encounter!.Combatants, c => c.Kind == "Adversary");
        Assert.Single((await Read(campaign.Code, campaign.DmKey)).Encounter!.Combatants,
                      c => c.Kind == "Adversary");
    }

    [Fact]
    public async Task AnUnrevealedMonstersHitPointsAreAbsentTooAndTheDmsArePresent()
    {
        var (campaign, _) = await WithOneMonster(revealed: false);

        Assert.Contains("50", WithoutIds(await Read(campaign.Code, campaign.DmKey)));
        Assert.DoesNotContain("50", WithoutIds(await Read(campaign.Code)));
    }

    // The document's open decisions offer a coarse health band as an alternative and then state
    // that the default is nothing. This asserts nothing: no band, no fraction, no stat line.
    [Fact]
    public async Task APlayerIsToldNothingAtAllAboutARevealedMonstersHealth()
    {
        var (campaign, _) = await WithOneMonster(revealed: true);

        var monster = Assert.Single((await Read(campaign.Code)).Encounter!.Combatants,
                                    c => c.Kind == "Adversary");

        Assert.Null(monster.Monster);
        Assert.Equal(MonsterName, monster.Name);
        Assert.True(monster.Revealed);

        // Nothing anywhere in the payload names health, which is what rules out a band added
        // later under a different word.
        var forPlayer = WithoutIds(await Read(campaign.Code));
        foreach (var word in new[] { "Bloodied", "Wounded", "Healthy", "Band", "Fraction" })
        {
            Assert.DoesNotContain(word, forPlayer, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Damaging the monster must not put the new number in a player's payload either, which is
    // the case a projection written per handler gets wrong on the second handler.
    [Fact]
    public async Task DamageToAMonsterDoesNotReachAPlayerThroughTheCommandsOwnAnswer()
    {
        var (campaign, monster) = await WithOneMonster(revealed: true);

        await using (var db = database.NewContext())
        {
            await new ChangeHitPointsHandler(db, Broadcaster).Handle(
                new ChangeHitPoints(campaign.Code, campaign.DmKey, monster, 37, HitPointDirection.Damage),
                default);
        }

        var answered = Broadcaster.Campaigns[^1];

        Assert.Contains("13", WithoutIds(answered.ForDm));
        Assert.DoesNotContain("13", WithoutIds(answered.ForPlayers));
        Assert.Null(Assert.Single(answered.ForPlayers.Encounter!.Combatants,
                                  c => c.Kind == "Adversary").Monster);
    }

    [Fact]
    public async Task OnlyTheDmMayChangeAMonstersHitPoints()
    {
        var (campaign, monster) = await WithOneMonster(revealed: true);

        await using var db = database.NewContext();

        await Assert.ThrowsAsync<NotTheDmException>(() => new ChangeHitPointsHandler(db, Broadcaster)
            .Handle(new ChangeHitPoints(campaign.Code, null, monster, 5, HitPointDirection.Damage), default));
    }
}
