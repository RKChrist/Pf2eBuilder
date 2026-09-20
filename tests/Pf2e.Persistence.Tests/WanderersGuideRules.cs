using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// A Wanderer's Guide export, which is a different format from a Pathbuilder one and the one the
/// owner's own character is in: a level 13 human rogue.
/// <para>The fixture is the owner's real export with the parts the importer never reads taken
/// out. It was thirteen megabytes; the parts that matter are eighty-four kilobytes, and the rest
/// was the full record of every item and spell the character has touched.</para>
/// </summary>
public class WanderersGuideRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    RecordingBroadcaster Broadcaster { get; } = new();

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    async Task<CharacterSheetView> Import()
    {
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        return await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, Fixture("einar-wanderers-guide.json")), default);
    }

    [Fact]
    public async Task TheFormatIsRecognisedWithoutBeingToldWhichItIs()
    {
        var einar = await Import();

        Assert.Equal("(lvl 13) Einar", einar.Name);
        Assert.Equal(13, einar.Level);
        Assert.Equal("Rogue", einar.ClassName);
        Assert.Equal("Human", einar.AncestryName);
    }

    [Fact]
    public async Task AttributesArriveAsModifiersBecauseThatIsWhatTheExportStates()
    {
        var einar = (await Import()).Build;

        // Wanderer's Guide gives modifiers rather than scores, so nothing is converted and
        // nothing can be converted wrongly.
        Assert.Equal(0, einar.Strength);
        Assert.Equal(5, einar.Dexterity);
        Assert.Equal(3, einar.Constitution);
        Assert.Equal(0, einar.Intelligence);
        Assert.Equal(3, einar.Wisdom);
        Assert.Equal(4, einar.Charisma);
    }

    [Fact]
    public async Task EverySkillComesThroughWithItsOwnRank()
    {
        var einar = await Import();

        // The sixteen named skills plus the one Lore this character has.
        Assert.Equal(17, einar.Skills.Count);

        var named = einar.Skills.Select(skill => skill.Name).ToList();
        foreach (var skill in new[]
        {
            "Acrobatics", "Arcana", "Athletics", "Crafting", "Deception", "Diplomacy",
            "Intimidation", "Medicine", "Nature", "Occultism", "Performance", "Religion",
            "Society", "Stealth", "Survival", "Thievery",
        })
        {
            Assert.Contains(skill, named);
        }

        // A Lore arrives as SKILL_LORE_UNDERWORLD and reads the way a sheet writes it.
        Assert.Contains("Underworld Lore", named);

        // And the empty SKILL_LORE____ slot is not a lore called nothing.
        Assert.DoesNotContain(named, name => name.Trim() == "Lore");
    }

    [Fact]
    public async Task AndWithTheNumbersTheExportComputed()
    {
        var einar = await Import();

        int Skill(string name) => einar.Skills.Single(s => s.Name == name).Value.Total;

        // Master Stealth: level 13 + rank 6 + Dexterity 5.
        Assert.Equal("Master", einar.Skills.Single(s => s.Name == "Stealth").Rank);
        Assert.Equal(24, Skill("Stealth"));

        // Trained Survival: 13 + 2 + Wisdom 3. This is the one the owner asked about.
        Assert.Equal("Trained", einar.Skills.Single(s => s.Name == "Survival").Rank);
        Assert.Equal(18, Skill("Survival"));

        // Untrained Athletics is the attribute alone, and Strength is nothing.
        Assert.Equal("Untrained", einar.Skills.Single(s => s.Name == "Athletics").Rank);
        Assert.Equal(0, Skill("Athletics"));

        // Legendary Perception: 13 + 8 + Wisdom 3.
        Assert.Equal(24, einar.Perception.Total);
    }

    [Fact]
    public async Task CoverTracksAndTrackAreThingsThisRogueCanAttempt()
    {
        // The owner's question, answered on their own character: both want training in Survival
        // and this rogue is trained in Survival.
        var einar = await Import();

        var cover = einar.CanAttempt.Single(a => a.Key == "cover-tracks");
        Assert.True(cover.Met);
        Assert.Equal("Survival", cover.Skill);
        Assert.Equal(18, cover.Modifier);

        var track = einar.CanAttempt.Single(a => a.Key == "track");
        Assert.True(track.Met);
        Assert.Equal(18, track.Modifier);

        // And the other half of the question: Crafting is untrained, so Repair is shut.
        var repair = einar.CanAttempt.Single(a => a.Key == "repair");
        Assert.False(repair.Met);
        Assert.Equal("Crafting", repair.Skill);
    }

    [Fact]
    public async Task TheStatedMaximumHitPointsAreTakenRatherThanReassembled()
    {
        var einar = await Import();

        // The export states 151 and no split into ancestry and class hit points, so there is
        // nothing to add up and a guessed split would only multiply back to the same number.
        Assert.Equal(151, einar.MaxHitPoints);
        Assert.Equal(151, einar.CurrentHitPoints);
    }

    // The screen asked for the two parts and this export states neither: both moved, both
    // saved, and the maximum stayed where it was. What the screen offers now is the number the
    // calculator actually reads.
    [Fact]
    public async Task TheStatedMaximumIsWhatAnEditCanMove()
    {
        var campaign = await NewCampaign();
        var einar = await ImportInto(campaign.Code);

        Assert.Equal(151, einar.Build.StatedMaxHitPoints);

        var parts = await Edit(campaign.Code, einar.Id,
            einar.Build with { AncestryHitPoints = 8, ClassHitPoints = 8 });
        Assert.Equal(151, parts.MaxHitPoints);

        var corrected = await Edit(campaign.Code, einar.Id,
            einar.Build with { StatedMaxHitPoints = 160 });
        Assert.Equal(160, corrected.MaxHitPoints);
        Assert.Equal(160, corrected.Build.StatedMaxHitPoints);

        // The session layer is not what was edited, so the damage a character is carrying
        // survives a correction to their maximum.
        Assert.Equal(151, corrected.CurrentHitPoints);
    }

    [Fact]
    public async Task ArmourCarriesItsOwnBonusAndCap()
    {
        var einar = await Import();

        Assert.Equal("Leather Armor", einar.Build.ArmorName);
        Assert.Equal("Expert", einar.Build.ArmorRank);
        Assert.Equal(4, einar.Build.ArmorDexCap);

        // Leather is +1 and the armour carries a +2 potency rune, which raises the armour's own
        // item bonus rather than stacking beside it.
        Assert.Equal(3, einar.Build.ArmorItemBonus);

        // 10 + level 13 + expert 4 + the armour's 3 + Dexterity 5 capped at 4, which is the 34
        // Wanderer's Guide computed for itself.
        Assert.Equal(34, einar.ArmorClass.Total);
    }

    [Fact]
    public async Task TheWeaponKeepsTheExportsOwnAttackBonus()
    {
        var einar = await Import();

        var bow = Assert.Single(einar.Attacks);

        // "Hunter's Anthem" is a specific magic shortbow, so the card shows the name the player
        // gave it and the number the export computed for it.
        Assert.Equal("Hunter's Anthem", bow.Name);
        Assert.Equal(26, bow.Value.Total);
    }

    [Fact]
    public async Task FeatsComeThroughGroupedTheWayTheExportGroupsThem()
    {
        var einar = await Import();

        Assert.NotEmpty(einar.Feats);

        var kinds = einar.Feats.Select(feat => feat.Kind).Distinct().ToList();
        Assert.Contains("Class Feat", kinds);
        Assert.Contains("Ancestry Feat", kinds);
        Assert.Contains("Class Feature", kinds);
        Assert.Contains("Heritage", kinds);

        // A heritage is written at level -1, which is the export saying "not at a level".
        Assert.All(einar.Feats, feat => Assert.True(feat.Level >= 1, $"{feat.Name} at {feat.Level}"));

        // And they join to the ruleset like any other name.
        Assert.Contains(einar.Feats, feat => feat.RuleId is not null);
    }

    [Fact]
    public async Task ARogueWithNoSpellsGetsNoSpellRowsAtAll()
    {
        var einar = await Import();

        Assert.Empty(einar.Spells);

        // Spell attack is expert on this sheet, from a Trickster's Ace style innate, so the slot
        // is there; what is absent is a repertoire.
        Assert.NotNull(einar.SpellAttack);
    }

    [Fact]
    public async Task APathbuilderExportStillImportsUnchanged()
    {
        // The two formats are told apart by shape, so adding one must not disturb the other.
        await using var db = database.NewContext();
        var campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
        var gnibbo = await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(campaign.Code, Fixture("gnibbo.json")), default);

        Assert.Equal("Gnibbo", gnibbo.Name);
        Assert.Equal(76, gnibbo.MaxHitPoints);
        Assert.Equal(25, gnibbo.ArmorClass.Total);

        // Pathbuilder states the parts, so there is no total to take and the two fields the
        // screen offers such a character are the two that feed it.
        Assert.Null(gnibbo.Build.StatedMaxHitPoints);

        await using var edits = database.NewContext();
        var thicker = await new EditCharacterHandler(edits, Broadcaster).Handle(
            new EditCharacter(campaign.Code, gnibbo.Id, gnibbo.Build with { ClassHitPoints = 10 }),
            default);

        Assert.Equal(90, thicker.MaxHitPoints);
    }

    async Task<CreatedCampaignView> NewCampaign()
    {
        await using var db = database.NewContext();
        return await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
    }

    async Task<CharacterSheetView> ImportInto(string code)
    {
        await using var db = database.NewContext();
        return await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(code, Fixture("einar-wanderers-guide.json")), default);
    }

    async Task<CharacterSheetView> Edit(string code, Guid id, CharacterBuildEdit build)
    {
        await using var db = database.NewContext();
        return await new EditCharacterHandler(db, Broadcaster)
            .Handle(new EditCharacter(code, id, build), default);
    }
}
