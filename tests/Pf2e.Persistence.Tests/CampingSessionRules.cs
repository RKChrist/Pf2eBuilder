using System.Text.Json.Nodes;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain.Tracking;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// A camping session as the Kingmaker Companion Guide runs one: five steps, a Zone DC and an
/// Encounter DC, a Prepare Campsite result that decides what the rest of the evening allows,
/// Camping activities of two hours each, meals, watches and daily preparations.
/// <para>What is asserted here is the part five people talking lose track of, which is the part
/// the app exists to keep.</para>
/// </summary>
public class CampingSessionRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Undo { get; } = new();

    CampHandlers Camp(RulesDbContext db) => new(db, Undo, Broadcaster);

    async Task<(CreatedCampaignView Campaign, Guid Gnibbo, Guid Einar)> Party()
    {
        CreatedCampaignView campaign;
        await using (var db = database.NewContext())
        {
            campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        }

        var ids = new List<Guid>();
        foreach (var name in new[] { "Gnibbo", "Einar" })
        {
            var payload = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gnibbo.json")))!;
            payload["build"]!["name"] = name;
            await using var db = database.NewContext();
            ids.Add((await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
                .Handle(new ImportCharacter(campaign.Code, payload.ToJsonString()), default)).Id);
        }

        return (campaign, ids[0], ids[1]);
    }

    async Task<CampaignView> Take(CreatedCampaignView campaign, Guid who, string activity, string outcome = "Success")
    {
        await using var db = database.NewContext();
        return await Camp(db).Handle(new TakeCampingActivity(campaign.Code, null, who, activity, outcome), default);
    }

    async Task<CampaignView> Campsite(CreatedCampaignView campaign, string? outcome)
    {
        await using var db = database.NewContext();
        return await Camp(db).Handle(new RecordCampsite(campaign.Code, campaign.DmKey, outcome), default);
    }

    async Task<CampaignView> Supplies(CreatedCampaignView campaign, int basic, int special = 0)
    {
        await using var db = database.NewContext();
        return await Camp(db).Handle(new SetCampSupplies(campaign.Code, null, basic, special), default);
    }

    async Task<CampaignView> Meal(
        CreatedCampaignView campaign, Guid who, string? kind, Guid? recipe = null, string? ruleId = null)
    {
        await using var db = database.NewContext();
        return await Camp(db).Handle(new ChooseMeal(campaign.Code, null, who, kind, recipe, ruleId), default);
    }

    async Task<CampaignView> Rest(CreatedCampaignView campaign)
    {
        await using var db = database.NewContext();
        return await new RestForTheNightHandler(db, Undo, Broadcaster)
            .Handle(new RestForTheNight(campaign.Code, campaign.DmKey), default);
    }

    async Task<CampaignView> Break(CreatedCampaignView campaign)
    {
        await using var db = database.NewContext();
        return await Camp(db).Handle(new BreakCamp(campaign.Code, campaign.DmKey), default);
    }

    /// <summary>Straight out of the store through a fresh context, for asserting that something a
    /// refused command tried to write is not there.</summary>
    async Task<CampSiteView> Stored(CreatedCampaignView campaign)
    {
        await using var db = database.NewContext();
        return (await new GetCampaignHandler(db).Handle(new GetCampaign(campaign.Code, null), default)).Camp;
    }

    [Fact]
    public async Task ACampStartsAtTheFirstStepWithTheWatchesThisPartyWouldKeep()
    {
        var (campaign, _, _) = await Party();

        await using var db = database.NewContext();
        var camp = (await new GetCampaignHandler(db).Handle(new GetCampaign(campaign.Code, null), default)).Camp;

        Assert.Equal("PrepareCampsite", camp.Step);
        Assert.Null(camp.Campsite);
        Assert.True(camp.ActivitiesAllowed);

        // Two people: sixteen hours set aside, eight hours each on watch. The rules' own table.
        Assert.Equal(16 * 60, camp.RestMinutes);
        Assert.Equal(8 * 60, camp.EachWatchMinutes);
    }

    [Theory]
    [InlineData(2, 960, 480)]
    [InlineData(3, 720, 240)]
    [InlineData(4, 640, 160)]
    [InlineData(5, 600, 120)]
    [InlineData(6, 576, 96)]
    [InlineData(7, 560, 80)]
    [InlineData(8, 549, 69)]
    [InlineData(9, 540, 60)]
    [InlineData(12, 540, 60)]
    public void TheWatchesTableIsOneFormula(int group, int total, int each)
    {
        Assert.Equal((total, each), Watches.For(group));
    }

    [Fact]
    public async Task ACampingActivityTakesTwoHoursAndIsClosedOnceSomebodySucceeds()
    {
        var (campaign, gnibbo, einar) = await Party();

        var taken = await Take(campaign, gnibbo, "Tell Campfire Story");
        Assert.Equal(120, taken.ElapsedMinutes);
        Assert.Equal("Tell Campfire Story", Assert.Single(taken.Camp.Taken).Activity);

        var refused = await Assert.ThrowsAsync<CampRuleException>(() => Take(campaign, einar, "tell campfire story"));
        Assert.Contains("already succeeded", refused.Message);
    }

    [Fact]
    public async Task AFailedActivityIsStillOpenToSomebodyElse()
    {
        var (campaign, gnibbo, einar) = await Party();

        await Take(campaign, gnibbo, "Set Traps", "Failure");
        var second = await Take(campaign, einar, "Set Traps", "CriticalSuccess");

        Assert.Equal(2, second.Camp.Taken.Count);
    }

    [Fact]
    public async Task NobodyTakesMoreThanFourInADay()
    {
        var (campaign, gnibbo, _) = await Party();
        foreach (var activity in new[] { "Relax", "Set Alarms", "Maintain Armor", "Enhance Campfire" })
        {
            await Take(campaign, gnibbo, activity);
        }

        var refused = await Assert.ThrowsAsync<CampRuleException>(() => Take(campaign, gnibbo, "Water Hazards"));
        Assert.Contains("as many as a day allows", refused.Message);
    }

    [Fact]
    public async Task CookSpecialMealMayBeTriedAgainBecauseTheRulesSaySo()
    {
        var (campaign, gnibbo, einar) = await Party();

        await Take(campaign, gnibbo, "Cook Special Meal");
        Assert.Equal(2, (await Take(campaign, einar, "Cook Special Meal")).Camp.Taken.Count);
    }

    [Fact]
    public async Task TheCampsiteRollDecidesWhatTheEveningAllows()
    {
        var (campaign, gnibbo, _) = await Party();

        var perfect = (await Campsite(campaign, "CriticalSuccess")).Camp;
        Assert.Equal(perfect.EncounterDc + 2, perfect.EncounterDcTonight);

        var poor = (await Campsite(campaign, "Failure")).Camp;
        Assert.Equal(-2, poor.ActivityPenalty);
        Assert.Equal(poor.EncounterDc, poor.EncounterDcTonight);

        var mess = (await Campsite(campaign, "CriticalFailure")).Camp;
        Assert.False(mess.ActivitiesAllowed);
        var refused = await Assert.ThrowsAsync<CampRuleException>(() => Take(campaign, gnibbo, "Relax"));
        Assert.Contains("good enough to sleep in", refused.Message);
    }

    [Fact]
    public async Task ARecipeIsTheTablesOwnAndEachCharacterChoosesTheirOwnMeal()
    {
        var (campaign, gnibbo, einar) = await Party();
        var recipe = Guid.NewGuid();

        await using (var db = database.NewContext())
        {
            // A player, with no DM key: the cook writes down the recipe they learned.
            await Camp(db).Handle(new SaveCampEntry(
                campaign.Code, null, recipe, "Recipe", "Hearty Stew", "Success: +1 to Fortitude saves.", 18), default);
            await Camp(db).Handle(new ChooseMeal(campaign.Code, null, gnibbo, "SpecialMeal", recipe, null), default);
        }

        CampaignView after;
        await using (var db = database.NewContext())
        {
            after = await Camp(db).Handle(new ChooseMeal(campaign.Code, null, einar, "Rations", null, null), default);
        }

        Assert.Equal(18, Assert.Single(after.Camp.Book).Dc);
        Assert.Equal(recipe, after.Camp.Meals.Single(meal => meal.CharacterId == gnibbo).RecipeId);
        Assert.Equal("Rations", after.Camp.Meals.Single(meal => meal.CharacterId == einar).Kind);

        // Taking the recipe out of the book takes it off the plate of whoever was eating it.
        await using var last = database.NewContext();
        var removed = await Camp(last).Handle(new RemoveCampEntry(campaign.Code, null, recipe), default);
        Assert.DoesNotContain(removed.Camp.Meals, meal => meal.CharacterId == gnibbo);
    }

    [Fact]
    public async Task BreakingCampKeepsTheZoneTheBookAndTheLarderAndNothingElse()
    {
        var (campaign, gnibbo, _) = await Party();
        await using (var db = database.NewContext())
        {
            var camp = Camp(db);
            await camp.Handle(new SetCampZone(campaign.Code, campaign.DmKey, "Greenbelt", 16, 14), default);
            await camp.Handle(new SaveCampEntry(campaign.Code, null, Guid.NewGuid(), "Activity", "Sing Rounds", "Everybody sings.", null), default);
            await camp.Handle(new SetCampSupplies(campaign.Code, null, 12, 3), default);
        }

        await Campsite(campaign, "Success");
        await Take(campaign, gnibbo, "Relax");

        CampaignView next;
        await using (var db = database.NewContext())
        {
            next = await Camp(db).Handle(new BreakCamp(campaign.Code, campaign.DmKey), default);
        }

        Assert.Equal("PrepareCampsite", next.Camp.Step);
        Assert.Null(next.Camp.Campsite);
        Assert.Empty(next.Camp.Taken);
        Assert.Equal("Greenbelt", next.Camp.ZoneName);
        Assert.Equal((16, 14), (next.Camp.ZoneDc, next.Camp.EncounterDc));
        Assert.Single(next.Camp.Book);
        Assert.Equal((12, 3), (next.Camp.BasicIngredients, next.Camp.SpecialIngredients));

        // Two hours of Relax and half an hour of daily preparations.
        Assert.Equal(150, next.ElapsedMinutes);
    }

    [Fact]
    public async Task TheCampsiteAndBreakingCampAreTheDmsAndTakingAnActivityIsAnybodys()
    {
        var (campaign, gnibbo, _) = await Party();

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<NotTheDmException>(() =>
            Camp(db).Handle(new RecordCampsite(campaign.Code, null, "Success"), default));
        await Assert.ThrowsAsync<NotTheDmException>(() =>
            Camp(db).Handle(new BreakCamp(campaign.Code, null), default));

        Assert.Single((await Take(campaign, gnibbo, "Relax")).Camp.Taken);
    }

    [Fact]
    public async Task ABasicMealSpendsTwoIngredientsAndChangingItPutsThemBack()
    {
        var (campaign, gnibbo, einar) = await Party();
        await Supplies(campaign, 5);

        Assert.Equal(3, (await Meal(campaign, gnibbo, "BasicMeal")).Camp.BasicIngredientsLeft);
        Assert.Equal(1, (await Meal(campaign, einar, "BasicMeal")).Camp.BasicIngredientsLeft);

        Assert.Equal(3, (await Meal(campaign, gnibbo, "Rations")).Camp.BasicIngredientsLeft);

        var cleared = (await Meal(campaign, einar, null)).Camp;
        Assert.Equal(5, cleared.BasicIngredientsLeft);
        Assert.DoesNotContain(cleared.Meals, meal => meal.CharacterId == einar);
    }

    [Fact]
    public async Task ALarderThatCannotPayForABasicMealSaysSoInsteadOfAllowingIt()
    {
        var (campaign, gnibbo, einar) = await Party();
        await Supplies(campaign, 3);

        Assert.Equal(1, (await Meal(campaign, gnibbo, "BasicMeal")).Camp.BasicIngredientsLeft);

        var refused = await Assert.ThrowsAsync<CampRuleException>(() => Meal(campaign, einar, "BasicMeal"));
        Assert.Equal(
            "A basic meal takes 2 basic ingredients a serving. "
            + "Tonight's meals would take 4 from a larder of 3.",
            refused.Message);

        // A refusal that has already written is a refusal that did nothing, so the absence is the
        // assertion and it is read back out of the store rather than off the thrown command.
        var stored = await Stored(campaign);
        Assert.DoesNotContain(stored.Meals, meal => meal.CharacterId == einar);
        Assert.Equal(1, stored.BasicIngredientsLeft);
    }

    [Fact]
    public async Task ARecipeFromTheCampBooksWordsReachTheCharacterWhoAteIt()
    {
        const string does = "Success: +1 status bonus to Fortitude saves until your next daily preparations.";
        var (campaign, gnibbo, _) = await Party();
        var recipe = Guid.NewGuid();

        await using (var db = database.NewContext())
        {
            await Camp(db).Handle(
                new SaveCampEntry(campaign.Code, null, recipe, "Recipe", "Hearty Stew", does, 18), default);
        }

        var eaten = (await Meal(campaign, gnibbo, "SpecialMeal", recipe)).Camp.Meals.Single();
        Assert.Equal("Hearty Stew", eaten.RecipeName);
        Assert.Equal(does, eaten.Benefit);

        // The rules keep a meal's benefit until the next daily preparations, and a night's rest is
        // not those, so sleeping on it changes nothing.
        Assert.Equal(does, (await Rest(campaign)).Camp.Meals.Single().Benefit);

        var next = (await Break(campaign)).Camp;
        Assert.Empty(next.Meals);
        Assert.Equal("Hearty Stew", Assert.Single(next.Book).Name);
    }

    [Fact]
    public async Task AMealFromTheRulesetIsChosenByItsRecordAndCarriesNoWordsOfItsOwn()
    {
        var (campaign, gnibbo, _) = await Party();

        var eaten = (await Meal(campaign, gnibbo, "SpecialMeal", ruleId: "campsite-meal-1")).Camp.Meals.Single();
        Assert.Equal("campsite-meal-1", eaten.RuleId);
        Assert.Null(eaten.RecipeId);

        // The absence is the point. The licence withholds a seeded meal's prose, so a projection
        // that put a name or a benefit here would be inventing one.
        Assert.Null(eaten.RecipeName);
        Assert.Null(eaten.Benefit);

        Assert.Equal("campsite-meal-1", (await Rest(campaign)).Camp.Meals.Single().RuleId);
        Assert.Empty((await Break(campaign)).Camp.Meals);
    }

    [Fact]
    public async Task TheLarderCannotBeSetBelowWhatTonightsMealsHaveAlreadyTaken()
    {
        var (campaign, gnibbo, einar) = await Party();
        await Supplies(campaign, 6);
        await Meal(campaign, gnibbo, "BasicMeal");
        await Meal(campaign, einar, "BasicMeal");

        var refused = await Assert.ThrowsAsync<CampRuleException>(() => Supplies(campaign, 3));
        Assert.Equal(
            "Tonight's meals have already taken 4 basic ingredients out of the larder.", refused.Message);

        Assert.Equal(6, (await Stored(campaign)).BasicIngredients);
    }

    [Fact]
    public void ChoosingASpecialMealWithoutSayingWhichIsRefused()
    {
        var validator = new ChooseMealValidator();
        var who = Guid.NewGuid();
        var recipe = Guid.NewGuid();

        ChooseMeal Choosing(string? kind, Guid? id, string? ruleId) =>
            new("ABCDEF", null, who, kind, id, ruleId);

        var neither = validator.Validate(Choosing("SpecialMeal", null, null));
        Assert.False(neither.IsValid);
        Assert.Contains("Say which special meal.", neither.Errors.Select(e => e.ErrorMessage));

        Assert.False(validator.Validate(Choosing("SpecialMeal", recipe, "campsite-meal-1")).IsValid);

        // The positive controls, so this test cannot pass on a validator that refuses everything.
        Assert.True(validator.Validate(Choosing("SpecialMeal", recipe, null)).IsValid);
        Assert.True(validator.Validate(Choosing("SpecialMeal", null, "campsite-meal-1")).IsValid);
        Assert.True(validator.Validate(Choosing("Rations", null, null)).IsValid);
        Assert.True(validator.Validate(Choosing(null, null, null)).IsValid);

        // Nothing but a special meal names a meal at all.
        Assert.False(validator.Validate(Choosing("Rations", null, "campsite-meal-1")).IsValid);
    }

    [Fact]
    public async Task UndoPutsTheWholeEveningBack()
    {
        var (campaign, gnibbo, _) = await Party();
        await Take(campaign, gnibbo, "Relax");

        await using var db = database.NewContext();
        var undone = await new UndoLastChangeHandler(db, Undo, Broadcaster)
            .Handle(new UndoLastChange(campaign.Code, campaign.DmKey), default);

        Assert.Empty(undone.Camp.Taken);
        Assert.Equal(0, undone.ElapsedMinutes);
    }
}
