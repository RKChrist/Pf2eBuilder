using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// A day counter and one activity per character per day, through real handlers. The arithmetic
/// of the level-based DCs is asserted against the printed table in the behaviour suite; this is
/// about what the day turning does and what it clears.
/// </summary>
public class DowntimeFlowRules(SeededDatabase database) : IClassFixture<SeededDatabase>
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
        return await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(code, payload.ToJsonString()), default);
    }

    async Task<CampaignView> Spend(
        CreatedCampaignView campaign, Guid character, string? activity, int? taskLevel, bool asDm = true)
    {
        await using var db = database.NewContext();
        return await new SetDowntimeActivityHandler(db, Undo, Broadcaster).Handle(
            new SetDowntimeActivity(
                campaign.Code, asDm ? campaign.DmKey : null, character, activity, taskLevel),
            default);
    }

    async Task<CampaignView> NextDay(CreatedCampaignView campaign, bool asDm = true)
    {
        await using var db = database.NewContext();
        return await new AdvanceDayHandler(db, Undo, Broadcaster)
            .Handle(new AdvanceDay(campaign.Code, asDm ? campaign.DmKey : null), default);
    }

    [Fact]
    public async Task ACampaignStartsOnDayOne()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, "Gnibbo");

        await using var db = database.NewContext();
        var read = await new GetCampaignHandler(db)
            .Handle(new GetCampaign(campaign.Code, campaign.DmKey), default);

        Assert.Equal(1, read.Day);
    }

    [Fact]
    public async Task AnActivityCarriesItsTaskLevelAndTheDcThatComesFromIt()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var spent = await Spend(campaign, gnibbo.Id, "earn-income", 7);
        var character = spent.Characters.Single();

        Assert.Equal("earn-income", character.DowntimeActivity);
        Assert.Equal(7, character.DowntimeTaskLevel);

        // The screen never carries the table; the DC comes down with the character.
        Assert.Equal(LevelBasedDc.For(7), character.DowntimeDc);
        Assert.Equal(23, character.DowntimeDc);
    }

    [Fact]
    public async Task ClearingTheActivityClearsTheTaskLevelWithIt()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        await Spend(campaign, gnibbo.Id, "craft", 4);

        var idle = (await Spend(campaign, gnibbo.Id, null, 4)).Characters.Single();

        // A task level with no activity would sit there looking like a DC for whatever gets
        // picked next.
        Assert.Null(idle.DowntimeActivity);
        Assert.Null(idle.DowntimeTaskLevel);
        Assert.Null(idle.DowntimeDc);
    }

    [Fact]
    public async Task TheDayTurningClearsWhatEverybodyChose()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");
        var seelah = await Import(campaign.Code, "Seelah");
        await Spend(campaign, gnibbo.Id, "earn-income", 5);
        await Spend(campaign, seelah.Id, "subsist", 3);

        var tomorrow = await NextDay(campaign);

        Assert.Equal(2, tomorrow.Day);
        Assert.All(tomorrow.Characters, character =>
        {
            Assert.Null(character.DowntimeActivity);
            Assert.Null(character.DowntimeTaskLevel);
            Assert.Null(character.DowntimeDc);
        });
    }

    [Fact]
    public async Task ADayIsNotAnHourSoTheCampClockDoesNotMove()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, "Gnibbo");

        var tomorrow = await NextDay(campaign);

        // Downtime is counted in days and camp in minutes. One number in one unit would make one
        // of the two screens do arithmetic to answer the question it exists for.
        Assert.Equal(2, tomorrow.Day);
        Assert.Equal(0, tomorrow.ElapsedMinutes);
    }

    [Fact]
    public async Task AnyoneAtTheTableMaySayWhatTheyAreDoingWithTheirDay()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var asPlayer = await Spend(campaign, gnibbo.Id, "retrain", null, asDm: false);

        Assert.Equal("Player", asPlayer.Role);
        Assert.Equal("retrain", asPlayer.Characters.Single().DowntimeActivity);
    }

    [Fact]
    public async Task OnlyTheDmTurnsTheDay()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, "Gnibbo");

        await Assert.ThrowsAsync<NotTheDmException>(() => NextDay(campaign, asDm: false));
    }

    [Fact]
    public async Task ATaskLevelOffTheTableIsRefused()
    {
        var campaign = await NewCampaign();
        var gnibbo = await Import(campaign.Code, "Gnibbo");

        var result = new SetDowntimeActivityValidator()
            .Validate(new SetDowntimeActivity(campaign.Code, null, gnibbo.Id, "craft", 40));

        Assert.False(result.IsValid);
    }
}
