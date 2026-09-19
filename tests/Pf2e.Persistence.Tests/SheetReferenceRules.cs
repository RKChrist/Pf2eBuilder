using Pf2e.Application.Features.Rules;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The party's own feats and spells, joined to the records the ruleset holds so the reference
/// screen can open the rule rather than only printing the name.
/// </summary>
public class SheetReferenceRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    RecordingBroadcaster Broadcaster { get; } = new();

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A campaign has to exist before a character can go into it, and its code is the
    /// one it was given rather than one a test picked.</summary>
    async Task<CharacterSheetView> Import()
    {
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        return await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, Fixture("gnibbo.json")), default);
    }

    [Fact]
    public async Task TheFeatsOnTheExportComeThroughWithTheirKindAndLevel()
    {
        var gnibbo = await Import();

        Assert.Equal(10, gnibbo.Feats.Count);

        var lore = gnibbo.Feats.Single(feat => feat.Name == "Bardic Lore");
        Assert.Equal("Class Feat", lore.Kind);
        Assert.Equal(1, lore.Level);

        // The kinds are the export's own words, which is what a character sheet is grouped by.
        Assert.Contains(gnibbo.Feats, feat => feat.Kind == "Ancestry Feat");
        Assert.Contains(gnibbo.Feats, feat => feat.Kind == "Skill Feat");
    }

    [Fact]
    public async Task AndAreJoinedToTheSeededFeatSoTheRuleOpens()
    {
        var gnibbo = await Import();

        var lore = gnibbo.Feats.Single(feat => feat.Name == "Bardic Lore");
        Assert.NotNull(lore.RuleId);
        Assert.StartsWith("feat-", lore.RuleId);

        await using var db = database.NewContext();
        var record = await new GetRuleHandler(db)
            .Handle(new GetRule(lore.RuleId!), default);

        Assert.Equal("Bardic Lore", record!.Summary.Name);
    }

    [Fact]
    public async Task EverySpellInTheRepertoireComesThroughByRank()
    {
        var gnibbo = await Import();

        Assert.NotEmpty(gnibbo.Spells);

        var shield = gnibbo.Spells.Single(spell => spell.Name == "Shield");
        Assert.Equal("Cantrip", shield.Kind);
        Assert.Equal(0, shield.Level);

        var invisibility = gnibbo.Spells.Single(spell => spell.Name == "Invisibility");
        Assert.Equal("Rank 2", invisibility.Kind);

        // A spell joins to the spell record and not to anything else called Shield.
        Assert.StartsWith("spell-", shield.RuleId);
    }

    [Fact]
    public async Task APreRemasterNameResolvesThroughTheRenameIndex()
    {
        // This bard's export was written before the Remaster, so it says Inspire Competence and
        // Dimension Door. Both were renamed rather than removed, and a sheet that showed the old
        // name and opened nothing was telling the player their own feat did not exist.
        var gnibbo = await Import();

        var competence = gnibbo.Feats.Single(feat => feat.Name == "Inspire Competence");
        Assert.NotNull(competence.RuleId);

        var door = gnibbo.Spells.Single(spell => spell.Name == "Dimension Door");
        Assert.NotNull(door.RuleId);

        // The record it opens is the one it became, under its current name.
        await using var db = database.NewContext();
        var get = new GetRuleHandler(db);
        Assert.Equal("Uplifting Overture", (await get.Handle(new GetRule(competence.RuleId!), default))!.Summary.Name);
        Assert.Equal("Translocate", (await get.Handle(new GetRule(door.RuleId!), default))!.Summary.Name);

        // The name on the sheet stays the one the player typed, because that is what their
        // character sheet says and renaming it under them would be its own surprise.
        Assert.Equal("Inspire Competence", competence.Name);
    }

    [Fact]
    public async Task ARenameIsNeverPreferredOverANameThatStillResolves()
    {
        // The index holds only names that resolve to nothing, and the direct match is tried
        // first regardless, so a current name can never be redirected by an old one.
        var gnibbo = await Import();

        var lore = gnibbo.Feats.Single(feat => feat.Name == "Bardic Lore");

        await using var db = database.NewContext();
        var record = await new GetRuleHandler(db).Handle(new GetRule(lore.RuleId!), default);
        Assert.Equal("Bardic Lore", record!.Summary.Name);
    }

    [Fact]
    public async Task ANameTheRulesetDoesNotHoldStillShowsAndSimplyDoesNotOpen()
    {
        // Homebrew is somebody's character too, and a feat printed after the snapshot was taken
        // is not the player's mistake. Dropping it would make a sheet lie about what it has.
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);

        var payload = System.Text.Json.Nodes.JsonNode.Parse(Fixture("gnibbo.json"))!;
        var feats = payload["build"]!["feats"]!.AsArray();
        feats.Add(new System.Text.Json.Nodes.JsonArray("Punch The Moon", null, "Class Feat", 9));

        var imported = await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, payload.ToJsonString()), default);

        var homebrew = imported.Feats.Single(feat => feat.Name == "Punch The Moon");
        Assert.Null(homebrew.RuleId);
        Assert.Equal("Class Feat", homebrew.Kind);
    }

    [Fact]
    public async Task WhatACharacterCanAttemptIsDecidedFromTheirOwnRanks()
    {
        var gnibbo = await Import();

        Assert.NotEmpty(gnibbo.CanAttempt);

        // The design document's worked example, on a real sheet. Decipher Writing wants training
        // in one of four, and this bard is trained in Society and untrained in Arcana, so it is
        // open to him. The skill he would actually roll is neither of those: he is expert in
        // Occultism, and the requirement being met and the roll being made are two questions.
        var decipher = gnibbo.CanAttempt.Single(a => a.Key == "decipher-writing");
        Assert.True(decipher.Met);
        Assert.Equal("Trained", decipher.Required);
        Assert.Equal("Occultism", decipher.Skill);
        Assert.Equal(gnibbo.Skills.Single(s => s.Name == "Occultism").Value.Total, decipher.Modifier);

        // And the other half: he is untrained in Medicine, so Treat Wounds is not.
        var treat = gnibbo.CanAttempt.Single(a => a.Key == "treat-wounds");
        Assert.False(treat.Met);
        Assert.Equal("Medicine", treat.Skill);
    }

    [Fact]
    public async Task AndCarriesTheNumberTheyWouldRoll()
    {
        var gnibbo = await Import();

        // Seek is rolled with Perception, which is not a skill, so a list that only looked at
        // skills would have answered zero.
        var seek = gnibbo.CanAttempt.Single(a => a.Key == "seek");
        Assert.Equal("Perception", seek.Skill);
        Assert.Equal(gnibbo.Perception.Total, seek.Modifier);

        // A skill action carries that skill's own total, with everything already on it.
        var demoralize = gnibbo.CanAttempt.Single(a => a.Key == "demoralize");
        Assert.Equal("Intimidation", demoralize.Skill);
        Assert.Equal(
            gnibbo.Skills.Single(s => s.Name == "Intimidation").Value.Total,
            demoralize.Modifier);
    }

    [Fact]
    public async Task AndFollowsTheSheetWhenTheSheetChanges()
    {
        // The list is computed rather than stored, so training in Medicine opens Treat Wounds
        // without anything else being touched.
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        var gnibbo = await new ImportCharacterHandler(db, db, Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, Fixture("gnibbo.json")), default);

        Assert.False(gnibbo.CanAttempt.Single(a => a.Key == "treat-wounds").Met);

        var trained = await new EditCharacterHandler(db, Broadcaster).Handle(
            new EditCharacter(campaign.Code, gnibbo.Id, gnibbo.Build with { Perception = "Master" }),
            default);

        // Perception rose, so Seek reads higher, and Treat Wounds is still shut because Medicine
        // did not move.
        Assert.True(
            trained.CanAttempt.Single(a => a.Key == "seek").Modifier
            > gnibbo.CanAttempt.Single(a => a.Key == "seek").Modifier);
        Assert.False(trained.CanAttempt.Single(a => a.Key == "treat-wounds").Met);
    }

    [Fact]
    public async Task ARepeatedCantripIsOneSpellRatherThanTwo()
    {
        var gnibbo = await Import();

        var names = gnibbo.Spells.Select(spell => (spell.Name, spell.Level)).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
