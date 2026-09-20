using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Persistence.Tests;

/// <summary>Wanderer's Guide as a dictionary. Anything not in it is a character that does not
/// exist, and the two refusals are asked for by number.</summary>
sealed class SharedCharacters : IWanderersGuideClient
{
    public const int Private = 403;
    public const int Down = 503;

    public Dictionary<int, string> Shared { get; } = [];

    public int Asked { get; private set; }

    public Task<WanderersGuideAnswer> FindCharacterAsync(WanderersGuideCharacterId id, CancellationToken ct)
    {
        Asked++;
        return Task.FromResult<WanderersGuideAnswer>(id.Value switch
        {
            Private => new WanderersGuideAnswer.NotShared(),
            Down => new WanderersGuideAnswer.Unreachable(),
            _ when Shared.TryGetValue(id.Value, out var json) => new WanderersGuideAnswer.Found(json),
            _ => new WanderersGuideAnswer.NotFound(),
        });
    }
}

/// <summary>
/// A character read from a Wanderer's Guide link, which is the owner's own character again: the
/// same level 13 rogue the export fixture holds, as their API returns it.
/// <para>The fixture is that character object with everything the importer does not read taken
/// out. What is left is the name, the level, the class and ancestry, and the numbers Wanderer's
/// Guide keeps beside a character for its own campaign view.</para>
/// </summary>
public class SharedCharacterRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    RecordingBroadcaster Broadcaster { get; } = new();

    SharedCharacters WanderersGuide { get; } = new()
    {
        Shared = { [70618] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "einar-shared.json")) },
    };

    async Task<CharacterSheetView> Import(string pasted)
    {
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        return await new ImportCharacterHandler(db, db, WanderersGuide, Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, pasted), default);
    }

    [Theory]
    [InlineData("https://wanderersguide.app/stat-block/character/70618")]
    [InlineData("https://wanderersguide.app/sheet/70618")]
    [InlineData("  https://www.wanderersguide.app/stat-block/character/70618/  ")]
    [InlineData("70618")]
    public async Task ALinkBringsTheCharacterItPointsAt(string pasted)
    {
        var einar = await Import(pasted);

        Assert.Equal("(lvl 13) Einar", einar.Name);
        Assert.Equal(13, einar.Level);
        Assert.Equal("Rogue", einar.ClassName);
        Assert.Equal("Human", einar.AncestryName);
    }

    [Fact]
    public async Task TheNumbersAreTheOnesWanderersGuideShowsAndNotOnesRebuiltFromGuessedAttributes()
    {
        var einar = await Import("https://wanderersguide.app/stat-block/character/70618");

        Assert.Equal(151, einar.MaxHitPoints);
        Assert.Equal(34, einar.ArmorClass.Total);
        Assert.Equal(21, einar.Fortitude.Total);
        Assert.Equal(27, einar.Reflex.Total);
        Assert.Equal(21, einar.Will.Total);
        Assert.Equal(24, einar.Perception.Total);
        Assert.Equal(24, einar.Skills.Single(skill => skill.Name == "Stealth").Value.Total);
        Assert.Equal(15, einar.Skills.Single(skill => skill.Name == "Underworld Lore").Value.Total);
    }

    [Fact]
    public async Task TheirDcLeavesTheTenOutAndOursCarriesIt()
    {
        var einar = await Import("70618");

        Assert.Equal(32, einar.ClassDc.Total);
        Assert.Equal(31, einar.SpellDc!.Total);
    }

    [Fact]
    public void AStatedSaveStillTakesAConditionOnTopOfIt()
    {
        var build = new Character(
            "Einar", 13, "Rogue", "Human", AttributeKind.Strength, new AttributeModifiers(0, 0, 0, 0, 0, 0),
            ProficiencyRank.Expert, ProficiencyRank.Legendary, ProficiencyRank.Expert, ProficiencyRank.Legendary,
            ProficiencyRank.Expert, ProficiencyRank.Untrained, "Unarmored", 0, null, 0, 0, 0, 0)
        {
            Stated = new StatedTotals(Fortitude: 21),
        };
        var frightened = SessionState.Fresh(151) with
        {
            Effects = [new ActiveEffect(Guid.NewGuid(), "Frightened", 2, new EffectSource.Seeded(Conditions.Frightened.Key))],
        };

        Assert.Equal(19, CharacterSheet.Compute(build, frightened).Fortitude.Total);
    }

    [Theory]
    [InlineData("https://evil.example/stat-block/character/70618")]
    [InlineData("https://wanderersguide.app.evil.example/stat-block/character/70618")]
    [InlineData("https://wanderersguide.app@evil.example/stat-block/character/70618")]
    [InlineData("https://evil.example/?next=https://wanderersguide.app/stat-block/character/70618")]
    [InlineData("ftp://wanderersguide.app/stat-block/character/70618")]
    [InlineData("https://wanderersguide.app/stat-block/character/not-a-number")]
    [InlineData("https://wanderersguide.app/stat-block/character/0")]
    public async Task AnythingThatIsNotALinkToACharacterThereIsNeverAskedAbout(string pasted)
    {
        Assert.False(WanderersGuideCharacterId.TryParse(pasted, out _));

        await Assert.ThrowsAsync<PathbuilderFormatException>(() => Import(pasted));
        Assert.Equal(0, WanderersGuide.Asked);
    }

    [Theory]
    [InlineData(SharedCharacters.Private, "make it public")]
    [InlineData(SharedCharacters.Down, "could not be reached")]
    [InlineData(12345, "no character numbered 12345")]
    public async Task EachRefusalSaysWhatToDoAboutIt(int id, string says)
    {
        var refusal = await Assert.ThrowsAsync<PathbuilderFormatException>(() => Import(id.ToString()));

        Assert.Contains(says, refusal.Message);
    }

    [Fact]
    public async Task ARefreshFromALinkKeepsTheAttacksAnEarlierFileBrought()
    {
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        var handler = new ImportCharacterHandler(db, db, WanderersGuide, Broadcaster);
        var export = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "einar-wanderers-guide.json"));

        var fromFile = await handler.Handle(new ImportCharacter(campaign.Code, export), default);
        var fromLink = await handler.Handle(new ImportCharacter(campaign.Code, "70618"), default);

        Assert.NotEmpty(fromFile.Attacks);
        Assert.Equal(fromFile.Attacks.Count, fromLink.Attacks.Count);
        Assert.Equal(fromFile.Feats.Count, fromLink.Feats.Count);
    }
}
