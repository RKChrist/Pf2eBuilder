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
    public async Task ARepeatedCantripIsOneSpellRatherThanTwo()
    {
        var gnibbo = await Import();

        var names = gnibbo.Spells.Select(spell => (spell.Name, spell.Level)).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
