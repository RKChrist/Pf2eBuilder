using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The camp panel through real handlers: the clock only goes up, the Treat Wounds hour is kept
/// rather than trusted to memory, and a night restores what a night restores.
/// </summary>
public class CampRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Undo { get; } = new();

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    async Task<CreatedCampaignView> NewCampaign()
    {
        await using var db = database.NewContext();
        return await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
    }

    async Task<CharacterSheetView> Import(string code, string name)
    {
        var payload = JsonNode.Parse(Fixture("gnibbo.json"))!;
        payload["build"]!["name"] = name;

        await using var db = database.NewContext();
        return await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(code, payload.ToJsonString()), default);
    }

    async Task<CampaignView> Camp(CreatedCampaignView campaign, Guid character, string activity)
    {
        await using var db = database.NewContext();
        return await new TakeCampActivityHandler(db, Undo, Broadcaster)
            .Handle(new TakeCampActivity(campaign.Code, campaign.DmKey, character, activity), default);
    }

    async Task<CampaignView> Rest(CreatedCampaignView campaign, bool asDm = true)
    {
        await using var db = database.NewContext();
        return await new RestForTheNightHandler(db, Undo, Broadcaster)
            .Handle(new RestForTheNight(campaign.Code, asDm ? campaign.DmKey : null), default);
    }

    async Task<CampaignView> Damage(CreatedCampaignView campaign, Guid character, int amount)
    {
        await using var db = database.NewContext();
        return await new ChangeHitPointsHandler(db, Undo, Broadcaster).Handle(
            new ChangeHitPoints(campaign.Code, campaign.DmKey, character, amount, HitPointDirection.Damage),
            default);
    }

    async Task<CampaignView> Apply(CreatedCampaignView campaign, Guid character, string key, int value)
    {
        await using var db = database.NewContext();
        return await new ApplyEffectHandler(db, Undo, Broadcaster).Handle(
            new ApplyEffect(
                campaign.Code, campaign.DmKey, Guid.NewGuid(),
                new EffectSpec(Effects.Find(key)!.Name, "Seeded", key, value, null, []),
                [new EffectTargetSpec("Character", character)]),
            default);
    }

    [Fact]
    public async Task TenMinutesAtATimeAndTheClockOnlyGoesUp()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        Assert.Equal(0, (await Camp(campaign, gnibbo.Id, "refocus")).ElapsedMinutes - 10);
        Assert.Equal(20, (await Camp(campaign, gnibbo.Id, "repair")).ElapsedMinutes);
        Assert.Equal(30, (await Camp(campaign, gnibbo.Id, "identify-magic")).ElapsedMinutes);
    }

    async Task<CampaignView> Pass(CreatedCampaignView campaign, int minutes, bool asDm = true)
    {
        await using var db = database.NewContext();
        return await new PassTimeHandler(db, Undo, Broadcaster)
            .Handle(new PassTime(campaign.Code, asDm ? campaign.DmKey : null, minutes), default);
    }

    [Fact]
    public async Task AnHourOnTheRoadEndsTheImmunityAnHourAtCampWouldHave()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Camp(campaign, gnibbo.Id, "treat-wounds");

        var halfway = await Pass(campaign, 30);
        Assert.Equal(40, halfway.ElapsedMinutes);
        Assert.Equal(30, halfway.Characters.Single().TreatWoundsImmuneFor);
        await Assert.ThrowsAsync<StillImmuneException>(() => Camp(campaign, gnibbo.Id, "treat-wounds"));

        Assert.Equal(0, (await Pass(campaign, 30)).Characters.Single().TreatWoundsImmuneFor);
        await Camp(campaign, gnibbo.Id, "treat-wounds");
    }

    [Fact]
    public async Task OnlyTheDmMovesTheWorldOnAndNotByMoreThanADay()
    {
        var campaign = await NewCampaign();

        await Assert.ThrowsAsync<NotTheDmException>(() => Pass(campaign, 10, asDm: false));
        Assert.False(new PassTimeValidator().Validate(new PassTime(campaign.Code, campaign.DmKey, 0)).IsValid);
        Assert.False(new PassTimeValidator().Validate(new PassTime(campaign.Code, campaign.DmKey, 24 * 60 + 1)).IsValid);
        Assert.True(new PassTimeValidator().Validate(new PassTime(campaign.Code, campaign.DmKey, 24 * 60)).IsValid);
    }

    [Fact]
    public async Task UndoPutsTheClockBackAlongWithEverythingElse()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Pass(campaign, 60);
        await Camp(campaign, gnibbo.Id, "treat-wounds");

        CampaignView undone;
        await using (var db = database.NewContext())
        {
            undone = await new UndoLastChangeHandler(db, Undo, Broadcaster)
                .Handle(new UndoLastChange(campaign.Code, campaign.DmKey), default);
        }

        // The ten minutes of Treat Wounds came back, and so did the mark it left: a clock put
        // back to before the treatment with the treatment still recorded read as immune for
        // seventy minutes.
        Assert.Equal(60, undone.ElapsedMinutes);
        Assert.Equal(0, undone.Characters.Single().TreatWoundsImmuneFor);
    }

    [Fact]
    public async Task TreatingSomebodyTwiceInTheSameHourIsRefusedWithTheTimeLeft()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var treated = await Camp(campaign, gnibbo.Id, "treat-wounds");
        Assert.Equal(60, treated.Characters.Single().TreatWoundsImmuneFor);

        var refused = await Assert.ThrowsAsync<StillImmuneException>(
            () => Camp(campaign, gnibbo.Id, "treat-wounds"));
        Assert.Contains("immune for another", refused.Message);

        // The immunity counts down as the party spends time on other things.
        await Camp(campaign, gnibbo.Id, "refocus");
        await Camp(campaign, gnibbo.Id, "refocus");
        await using var db = database.NewContext();
        var later = await new GetCampaignHandler(db)
            .Handle(new GetCampaign(campaign.Code, campaign.DmKey), default);
        Assert.Equal(40, later.Characters.Single().TreatWoundsImmuneFor);
    }

    [Fact]
    public async Task AndIsAllowedAgainOnceTheHourIsUp()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Camp(campaign, gnibbo.Id, "treat-wounds");

        // Six more ten-minute activities is an hour, which is exactly the immunity.
        for (var spent = 0; spent < 6; spent++)
        {
            await Camp(campaign, gnibbo.Id, "refocus");
        }

        var again = await Camp(campaign, gnibbo.Id, "treat-wounds");
        Assert.Equal(60, again.Characters.Single().TreatWoundsImmuneFor);
    }

    [Fact]
    public async Task TreatingOneCharacterLeavesTheRestOfThePartyTreatable()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        var seelah = await Import(campaign.Code, "Seelah");

        await Camp(campaign, gnibbo.Id, "treat-wounds");
        var second = await Camp(campaign, seelah.Id, "treat-wounds");

        // Seelah was just Treated, so her hour is whole. Gnibbo was Treated the ten minutes it
        // took to Treat her ago, so his is ten shorter, and neither of them is treatable.
        Assert.Equal(60, second.Characters.Single(c => c.Name == "Seelah").TreatWoundsImmuneFor);
        Assert.Equal(50, second.Characters.Single(c => c.Name == "Gnibbo").TreatWoundsImmuneFor);
    }

    [Fact]
    public async Task ANightRestoresHitPointsAndEndsWhatANightEnds()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Damage(campaign, gnibbo.Id, 40);
        await Apply(campaign, gnibbo.Id, "fatigued", 0);
        await Apply(campaign, gnibbo.Id, "drained", 2);
        await Apply(campaign, gnibbo.Id, "frightened", 2);

        var morning = await Rest(campaign);
        var character = morning.Characters.Single();

        // Level 7, Constitution +2, so fourteen back on 36 of 76.
        Assert.Equal(50, character.CurrentHitPoints);
        Assert.Equal(480, morning.ElapsedMinutes);

        var carried = character.Effects.Select(e => e.Key).ToList();
        Assert.DoesNotContain("fatigued", carried);
        Assert.Contains("drained", carried);
        Assert.Equal(1, character.Effects.Single(e => e.Key == "drained").Value);

        // Sleeping does not cure being frightened of what is outside the tent.
        Assert.Equal(2, character.Effects.Single(e => e.Key == "frightened").Value);
    }

    [Fact]
    public async Task ANightDoesNotHealPastFull()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Damage(campaign, gnibbo.Id, 3);

        var character = (await Rest(campaign)).Characters.Single();

        Assert.Equal(character.MaxHitPoints, character.CurrentHitPoints);
    }

    [Fact]
    public async Task AndClearsTheTreatWoundsImmunityBecauseAnHourHasCertainlyPassed()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Camp(campaign, gnibbo.Id, "treat-wounds");

        var morning = await Rest(campaign);

        Assert.Equal(0, morning.Characters.Single().TreatWoundsImmuneFor);
    }

    [Fact]
    public async Task OnlyTheDmCallsANight()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, "Gnibbo");

        await Assert.ThrowsAsync<NotTheDmException>(() => Rest(campaign, asDm: false));
    }
}
