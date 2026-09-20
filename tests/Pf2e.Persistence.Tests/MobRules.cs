using FluentValidation;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// Monsters as design/014 has them: a number the players can say out loud, monsters that are in
/// no book, several at once, and a line the DM can change mid-fight.
/// </summary>
public class MobRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    const string Doppelganger = "creature-126";

    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Undo { get; } = new();

    async Task<CreatedCampaignView> Campaign()
    {
        await using var db = database.NewContext();
        return await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
    }

    async Task<CampaignView> Add(CreatedCampaignView campaign, AddCombatant command)
    {
        await using var db = database.NewContext();
        return await new AddCombatantHandler(db, db, Undo, Broadcaster).Handle(command, default);
    }

    async Task<CampaignView> Read(CreatedCampaignView campaign, bool asDm)
    {
        await using var db = database.NewContext();
        return await new GetCampaignHandler(db).Handle(new GetCampaign(campaign.Code, asDm ? campaign.DmKey : null), default);
    }

    static AddCombatant Seeded(CreatedCampaignView campaign, int count = 1) =>
        new(campaign.Code, campaign.DmKey, Doppelganger, null, null, Count: count);

    [Fact]
    public async Task SeveralOfOneMonsterArriveTogetherAndAreNumberedForBothSidesOfTheScreen()
    {
        var campaign = await Campaign();
        await Add(campaign, Seeded(campaign, count: 3));

        var forDm = (await Read(campaign, asDm: true)).Encounter!.Combatants;
        var forPlayers = (await Read(campaign, asDm: false)).Encounter!.Combatants;

        Assert.Equal(["Mob 1", "Mob 2", "Mob 3"], forDm.Select(c => c.Monster!.Alias).Order());
        Assert.Equal(3, forDm.Select(c => c.Name).Distinct().Count());
        Assert.Equal(["Mob 1", "Mob 2", "Mob 3"], forPlayers.Select(c => c.Name).Order());
    }

    [Fact]
    public async Task MobTwoIsStillMobTwoAfterMobOneDies()
    {
        var campaign = await Campaign();
        var fight = (await Add(campaign, Seeded(campaign, count: 3))).Encounter!.Combatants;
        var first = fight.Single(c => c.Monster!.Alias == "Mob 1");
        var second = fight.Single(c => c.Monster!.Alias == "Mob 2");

        await using (var db = database.NewContext())
        {
            await new RemoveCombatantHandler(db, Undo, Broadcaster)
                .Handle(new RemoveCombatant(campaign.Code, campaign.DmKey, first.Id), default);
        }

        var after = (await Read(campaign, asDm: false)).Encounter!.Combatants;
        Assert.Equal("Mob 2", after.Single(c => c.Id == second.Id).Name);
        Assert.DoesNotContain(after, c => c.Name == "Mob 1");

        // The freed number goes to the next arrival, once nothing on the screen holds it.
        var reinforced = (await Add(campaign, Seeded(campaign))).Encounter!.Combatants;
        Assert.Equal(["Mob 1", "Mob 2", "Mob 3"], reinforced.Select(c => c.Monster!.Alias).Order());
    }

    [Fact]
    public async Task AMonsterThatIsInNoBookJoinsWithTheNumbersTheDmGaveIt()
    {
        var campaign = await Campaign();
        var own = new HomebrewMonster("Clockwork Heron", 64, Level: 5, ArmorClass: 22, Fortitude: 12, Reflex: 15, Will: 9, Perception: 13);

        var added = Assert.Single((await Add(campaign,
            new AddCombatant(campaign.Code, campaign.DmKey, null, null, null, Homebrew: own))).Encounter!.Combatants);

        Assert.Equal("Clockwork Heron", added.Name);
        Assert.Equal(64, added.Monster!.MaxHitPoints);
        Assert.Equal(64, added.Monster.CurrentHitPoints);
        Assert.Equal(22, added.Monster.ArmorClass);
        Assert.Equal(15, added.Monster.Reflex);
        Assert.Equal(string.Empty, added.Monster.RuleId);

        // And a player is told exactly as little about it as about one from the book.
        var mob = Assert.Single((await Read(campaign, asDm: false)).Encounter!.Combatants);
        Assert.Equal("Mob 1", mob.Name);
        Assert.Null(mob.Monster);
    }

    [Fact]
    public void ARequestNamesOneThingToAdd()
    {
        var validator = new AddCombatantValidator();
        var own = new HomebrewMonster("Heron", 10);

        Assert.True(validator.Validate(new AddCombatant("ABCD", null, null, null, null, Homebrew: own)).IsValid);
        Assert.False(validator.Validate(new AddCombatant("ABCD", null, Doppelganger, null, null, Homebrew: own)).IsValid);
        Assert.False(validator.Validate(new AddCombatant("ABCD", null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new AddCombatant("ABCD", null, Doppelganger, null, null, Count: 21)).IsValid);
        Assert.False(validator.Validate(new AddCombatant("ABCD", null, null, Guid.NewGuid(), null, Count: 2)).IsValid);
        Assert.False(validator.Validate(new AddCombatant("ABCD", null, null, null, null, Homebrew: own with { MaxHitPoints = 0 })).IsValid);
    }

    [Fact]
    public async Task TheDmChangesAMonstersLineAndItsHitPointsMoveWithItsMaximum()
    {
        var campaign = await Campaign();
        var monster = Assert.Single((await Add(campaign, Seeded(campaign))).Encounter!.Combatants);

        await using (var db = database.NewContext())
        {
            await new ChangeHitPointsHandler(db, Undo, Broadcaster).Handle(
                new ChangeHitPoints(campaign.Code, campaign.DmKey, monster.Id, 20, HitPointDirection.Damage), default);
        }

        var line = monster.Monster!;
        var elite = new EditMonsterRequest(
            "Elite Doppelganger", line.MaxHitPoints + 20, line.Level + 1, line.ArmorClass + 2,
            line.Fortitude + 2, line.Reflex + 2, line.Will + 2, line.Perception + 2);

        CampaignView answered;
        await using (var db = database.NewContext())
        {
            answered = await new EditMonsterHandler(db, Undo, Broadcaster)
                .Handle(new EditMonster(campaign.Code, campaign.DmKey, monster.Id, elite), default);
        }

        var changed = Assert.Single(answered.Encounter!.Combatants);
        Assert.Equal("Elite Doppelganger", changed.Name);
        Assert.Equal(70, changed.Monster!.MaxHitPoints);

        // Thirty of fifty became fifty of seventy: the wound is the same twenty.
        Assert.Equal(50, changed.Monster.CurrentHitPoints);
        Assert.Equal(line.ArmorClass + 2, changed.Monster.ArmorClass);
        Assert.Equal("Mob 1", changed.Monster.Alias);
    }

    [Fact]
    public async Task OnlyTheDmChangesAMonster()
    {
        var campaign = await Campaign();
        var monster = Assert.Single((await Add(campaign, Seeded(campaign))).Encounter!.Combatants);
        var same = new EditMonsterRequest("Anything", 1, 0, 0, 0, 0, 0, 0);

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<NotTheDmException>(() => new EditMonsterHandler(db, Undo, Broadcaster)
            .Handle(new EditMonster(campaign.Code, null, monster.Id, same), default));
    }
}
