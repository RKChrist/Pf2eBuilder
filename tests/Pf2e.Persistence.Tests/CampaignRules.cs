using System.Text.Json.Nodes;
using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Infrastructure.Persistence;
using Pf2e.Application.Features.Rules;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain.Rules;

namespace Pf2e.Persistence.Tests;

/// <summary>Records what a campaign would have been told, so a test can assert that it was.</summary>
sealed class RecordingBroadcaster : ICampaignBroadcaster
{
    public List<(string Code, CharacterSheetView Sheet)> Sent { get; } = [];

    public List<(string Code, CampaignModeView Mode)> Modes { get; } = [];

    public Task CharacterChangedAsync(string code, CharacterSheetView sheet, CancellationToken ct)
    {
        Sent.Add((code, sheet));
        return Task.CompletedTask;
    }

    public Task ModeChangedAsync(string code, CampaignModeView mode, CancellationToken ct)
    {
        Modes.Add((code, mode));
        return Task.CompletedTask;
    }

    /// <summary>Both projections are kept, so a test can assert what the players were sent and
    /// not only what the caller was answered.</summary>
    public Task CampaignChangedAsync(
        string code, CampaignView forDm, CampaignView forPlayers, CancellationToken ct)
    {
        Campaigns.Add((code, forDm, forPlayers));
        return Task.CompletedTask;
    }

    public List<(string Code, CampaignView ForDm, CampaignView ForPlayers)> Campaigns { get; } = [];
}

/// <summary>
/// The importer needs the seeded armour records to know what studded leather is worth, so it is
/// exercised here against a real database rather than against a substitute that would only
/// prove the test's own idea of the ruleset.
/// </summary>
public class CampaignRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    RecordingBroadcaster Broadcaster { get; } = new();

    MemoryUndoStack Undo { get; } = new();

    async Task<CreatedCampaignView> NewCampaign()
    {
        await using var db = database.NewContext();
        return await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
    }

    async Task<CharacterSheetView> Import(string code, string pathbuilder)
    {
        await using var db = database.NewContext();
        return await new ImportCharacterHandler(db, db, new SharedCharacters(), Broadcaster)
            .Handle(new ImportCharacter(code, pathbuilder), default);
    }

    /// <summary>A signed delta reads better in a test than an amount and a word, and the
    /// command's own shape is asserted separately.</summary>
    async Task<CharacterSheetView?> Damage(string code, Guid characterId, int delta, string? dmKey = null)
    {
        await using var db = database.NewContext();
        var campaign = await new ChangeHitPointsHandler(db, Undo, Broadcaster).Handle(
            new ChangeHitPoints(
                code, dmKey, characterId, Math.Abs(delta),
                delta < 0 ? HitPointDirection.Damage : HitPointDirection.Heal),
            default);

        return campaign.Characters.FirstOrDefault(c => c.Id == characterId);
    }

    /// <summary>Applies to one character, which is what every caller below wants. The target
    /// list is what makes the same row reach the whole party.</summary>
    async Task<CharacterSheetView?> Set(string code, Guid characterId, Guid application, EffectSpec? effect)
    {
        await using var db = database.NewContext();
        var campaign = await new ApplyEffectHandler(db, Undo, Broadcaster).Handle(
            new ApplyEffect(code, null, application, effect, [new EffectTargetSpec("Character", characterId)]),
            default);

        return campaign.Characters.FirstOrDefault(c => c.Id == characterId);
    }

    async Task<CampaignView> Read(string code, string? dmKey = null)
    {
        await using var db = database.NewContext();
        return await new GetCampaignHandler(db).Handle(new GetCampaign(code, dmKey), default);
    }

    async Task<CampaignModeView> Mode(string code, string? dmKey, string mode)
    {
        await using var db = database.NewContext();
        return await new SetModeHandler(db, Broadcaster).Handle(new SetMode(code, dmKey, mode), default);
    }

    static string Shape(CharacterSheetView sheet) =>
        System.Text.Json.JsonSerializer.Serialize(sheet with { Id = Guid.Empty });

    static EffectSpec Custom(string name, string type, int value, string stat) =>
        new(name, "Custom", null, 0, null,
            [new EffectModifierView(type, value, [new SelectorSpecView("Exactly", stat, null, null)])]);

    [Fact]
    public async Task ImportingTheGoblinBardGivesSeventySixHitPointsAndTwentyFiveArmorClass()
    {
        var campaign = await NewCampaign();
        var sheet = await Import(campaign.Code, Fixture("gnibbo.json"));

        Assert.Equal("Gnibbo", sheet.Name);
        Assert.Equal(76, sheet.MaxHitPoints);
        Assert.Equal(76, sheet.CurrentHitPoints);
        Assert.Equal(25, sheet.ArmorClass.Total);

        // The export states an acTotal of 24 and it is never read, because recomputing the
        // number from the build and the seeded ruleset is the point of this product.
        Assert.Contains("\"acTotal\": 24", Fixture("gnibbo.json"));

        // The armour's own bonus is one +3 item modifier, the seeded ac of 2 plus one potency
        // rune, rather than a +2 and a +1 the stacking rule would refuse to combine.
        var armor = Assert.Single(sheet.ArmorClass.Applied);
        Assert.Equal("Studded Leather Armor", armor.Source);
        Assert.Equal("Item", armor.Type);
        Assert.Equal(3, armor.Value);
        Assert.Equal(22, sheet.ArmorClass.Base);

        Assert.Equal((campaign.Code, sheet), Assert.Single(Broadcaster.Sent));
    }

    [Fact]
    public async Task ImportingBringsTheSkillsWeaponsAndSpellcastingTheExportAlreadyCarried()
    {
        var campaign = await NewCampaign();
        var sheet = await Import(campaign.Code, Fixture("gnibbo.json"));

        // Sixteen named skills and the two Lores the player wrote down.
        Assert.Equal(18, sheet.Skills.Count);
        Assert.Contains(sheet.Skills, skill => skill.Name == "Warfare Lore");

        // Performance is master at level 7 with Charisma 19: 7 + 6 + 4.
        var performance = sheet.Skills.Single(skill => skill.Name == "Performance");
        Assert.Equal("Master", performance.Rank);
        Assert.Equal(17, performance.Value.Total);

        // Athletics is untrained and Strength 10, so it is the attribute alone, which is zero.
        var athletics = sheet.Skills.Single(skill => skill.Name == "Athletics");
        Assert.Equal("Untrained", athletics.Rank);
        Assert.Equal(0, athletics.Value.Total);

        // The export states each weapon's finished bonus, and those are the numbers shown. The
        // bard is untrained in martial weapons on paper and hits at +15 with a rapier in fact,
        // so recomputing from the proficiency table would have shown +4.
        Assert.Equal(15, sheet.Attacks.Single(a => a.Name == "+1 Striking Rapier").Value.Total);
        Assert.Equal(14, sheet.Attacks.Single(a => a.Name == "Shortbow").Value.Total);

        // Occult, expert, Charisma 19: 7 + 4 + 4 for the attack and ten more for the DC.
        Assert.Equal("Occult", sheet.SpellTradition);
        Assert.Equal(15, sheet.SpellAttack!.Total);
        Assert.Equal(25, sheet.SpellDc!.Total);
    }

    [Fact]
    public async Task AFinesseRapierAndARangedBowAreBothDexterityGoverned()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));
        var before = character.Attacks.ToDictionary(a => a.Name, a => a.Value.Total);

        // Which attribute governs an attack is in the ruleset and not in the export. The rapier
        // is Finesse and this goblin has more Dexterity than Strength, so clumsy reaches it.
        var after = await Set(
            campaign.Code, character.Id, Guid.NewGuid(),
            new EffectSpec("Clumsy", "Seeded", "clumsy", 2, null, []));

        Assert.Equal(
            before["+1 Striking Rapier"] - 2,
            after!.Attacks.Single(a => a.Name == "+1 Striking Rapier").Value.Total);
        Assert.Equal(before["Shortbow"] - 2, after.Attacks.Single(a => a.Name == "Shortbow").Value.Total);
    }

    [Fact]
    public async Task ReImportingReplacesTheBuildAndLeavesTheSessionWhereItWas()
    {
        var campaign = await NewCampaign();
        var first = await Import(campaign.Code, Fixture("gnibbo.json"));
        await Damage(campaign.Code, first.Id, -30);

        var levelled = JsonNode.Parse(Fixture("gnibbo.json"))!;
        levelled["build"]!["level"] = 8;
        var second = await Import(campaign.Code, levelled.ToJsonString());

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(8, second.Level);
        Assert.Equal(86, second.MaxHitPoints);
        Assert.Equal(46, second.CurrentHitPoints);
        Assert.Equal(26, second.ArmorClass.Total);
    }

    [Fact]
    public async Task TwoHitPointChangesInSequenceSumOnTheStoredValue()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));

        await Damage(campaign.Code, character.Id, -10);
        var after = await Damage(campaign.Code, character.Id, -7);

        Assert.Equal(59, after!.CurrentHitPoints);
        Assert.Equal(59, Assert.Single((await Read(campaign.Code)).Characters).CurrentHitPoints);
    }

    // A creature nothing answers to is a refusal naming the id, not a silent success. An effect
    // applied to nobody would sit in the database looking applied and doing nothing.
    [Fact]
    public async Task AnAbsentCreatureIsRefusedByNameRatherThanQuietlyAccepted()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, Fixture("gnibbo.json"));
        var nobody = Guid.NewGuid();

        await Assert.ThrowsAsync<CombatantNotFoundException>(() => Damage(campaign.Code, nobody, -1));
        await Assert.ThrowsAsync<CombatantNotFoundException>(
            () => Set(campaign.Code, nobody, Guid.NewGuid(), Custom("Bless", "Status", 1, "Will")));

        // Removing an application that is not there still succeeds, because a retry of a removal
        // has to converge rather than fail.
        Assert.Null(await Set(campaign.Code, nobody, Guid.NewGuid(), null));
    }

    [Fact]
    public async Task EmptyingASlotRemovesItsEffectAndEmptyingAnEmptySlotStillSucceeds()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();

        var applied = await Set(campaign.Code, character.Id, slot,
            new EffectSpec("Clumsy", "Seeded", "clumsy", 2, "1 minute", []));
        Assert.Equal(23, applied!.ArmorClass.Total);

        var removed = await Set(campaign.Code, character.Id, slot, null);
        Assert.Empty(removed!.Effects);
        Assert.Equal(25, removed.ArmorClass.Total);

        var again = await Set(campaign.Code, character.Id, Guid.NewGuid(), null);
        Assert.Empty(again!.Effects);
    }

    [Fact]
    public async Task TheSameEffectSentTwiceLeavesOneEffect()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();
        var spec = new EffectSpec("Clumsy", "Seeded", "clumsy", 2, null, []);

        await Set(campaign.Code, character.Id, slot, spec);
        var second = await Set(campaign.Code, character.Id, slot, spec);

        Assert.Single(second!.Effects);
        Assert.Equal(23, second.ArmorClass.Total);
        Assert.Single(Assert.Single((await Read(campaign.Code)).Characters).Effects);
    }

    [Fact]
    public async Task TwoCustomEffectsInDifferentSlotsBothPersistAndBothReachTheSheet()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));

        await Set(campaign.Code, character.Id, Guid.NewGuid(), Custom("Heroism", "Status", 2, "Will"));
        await Set(campaign.Code, character.Id, Guid.NewGuid(), Custom("Resolve", "Circumstance", 1, "Will"));

        var stored = Assert.Single((await Read(campaign.Code)).Characters);

        Assert.Equal(2, stored.Effects.Count);
        Assert.Equal(15, stored.Will.Total);
        Assert.Equal(["Heroism", "Resolve"], stored.Will.Applied.Select(m => m.Source).Order());
        Assert.All(stored.Effects, effect => Assert.Equal("Custom", effect.Kind));
        Assert.Contains(stored.Effects, effect => effect.Modifiers.Single().Applies.Single().Stat == "Will");
    }

    [Fact]
    public async Task TwoCharactersInOneCampaignBothPersist()
    {
        var campaign = await NewCampaign();
        await Import(campaign.Code, Fixture("gnibbo.json"));

        var second = JsonNode.Parse(Fixture("gnibbo.json"))!;
        second["build"]!["name"] = "Tarrow";
        await Import(campaign.Code, second.ToJsonString());

        var read = await Read(campaign.Code);

        Assert.Equal(["Gnibbo", "Tarrow"], read.Characters.Select(c => c.Name));
        Assert.All(read.Characters, character => Assert.Equal(76, character.MaxHitPoints));
    }

    [Fact]
    public async Task AnExportMissingTheSixDriftingKeysImportsIdentically()
    {
        var one = await NewCampaign();
        var other = await NewCampaign();
        var full = await Import(one.Code, Fixture("gnibbo.json"));
        var vintage = await Import(other.Code, Fixture("gnibbo-vintage.json"));

        foreach (var key in new[] { "dualClass", "xp", "sizeName", "rituals", "resistances", "inventorMods" })
        {
            Assert.Contains($"\"{key}\"", Fixture("gnibbo.json"));
            Assert.DoesNotContain($"\"{key}\"", Fixture("gnibbo-vintage.json"));
        }

        // Compared as JSON rather than by record equality, because a record's equality over a
        // list is by reference and would pass for any two sheets at all.
        Assert.Equal(Shape(full), Shape(vintage));
    }

    [Fact]
    public async Task APayloadThatIsNotAnExportFailsWithASentenceAPlayerCanRead()
    {
        var campaign = await NewCampaign();

        var notJson = await Assert.ThrowsAsync<PathbuilderFormatException>(
            () => Import(campaign.Code, "paste your character here"));
        var noName = await Assert.ThrowsAsync<PathbuilderFormatException>(
            () => Import(campaign.Code, "{\"success\":true,\"build\":{\"level\":7}}"));

        // Every JSON file in this repo with a name in it used to be a character. This one
        // arrived as a level 1 "pf2e-tokens" with AC 10 and a single hit point, and could be
        // added to a fight.
        var notACharacter = await Assert.ThrowsAsync<PathbuilderFormatException>(
            () => Import(campaign.Code, """
                {"name":"pf2e-tokens","version":"1.0.0","private":true,
                 "scripts":{"build":"style-dictionary build"},
                 "devDependencies":{"style-dictionary":"^4.0.0"}}
                """));

        Assert.Contains("Pathbuilder", notJson.Message);
        Assert.Contains("no character name", noName.Message);
        Assert.Contains("pf2e-tokens", notACharacter.Message);
        Assert.Contains("no class, ancestry", notACharacter.Message);
        Assert.Empty((await Read(campaign.Code)).Characters);
    }

    // design/007 makes a campaign something you create. Importing into a code nobody created
    // used to start one, and that campaign's DM key went to nobody, so it had no DM.
    [Fact]
    public async Task ImportingIntoACampaignThatDoesNotExistFailsAndNamesTheProblem()
    {
        var failure = await Assert.ThrowsAsync<CampaignNotFoundException>(
            () => Import("NOBODY", Fixture("gnibbo.json")));

        Assert.Contains("NOBODY", failure.Message);
        Assert.Contains("has to be created", failure.Message);
        await Assert.ThrowsAsync<CampaignNotFoundException>(() => Read("NOBODY"));
    }

    [Fact]
    public async Task TheCreatorHoldsTheDmKeyAndEverybodyElseIsAPlayer()
    {
        var campaign = await NewCampaign();

        Assert.NotEmpty(campaign.DmKey);
        Assert.Equal("Exploration", campaign.Mode);
        Assert.Equal("Dm", (await Read(campaign.Code, campaign.DmKey)).Role);
        Assert.Equal("Player", (await Read(campaign.Code)).Role);
        Assert.Equal("Player", (await Read(campaign.Code, "not-the-key")).Role);
    }

    [Fact]
    public async Task OnlyTheDmChangesTheModeAndTheChangeReachesEverybody()
    {
        var campaign = await NewCampaign();

        await Assert.ThrowsAsync<NotTheDmException>(() => Mode(campaign.Code, null, "Encounter"));
        await Assert.ThrowsAsync<NotTheDmException>(() => Mode(campaign.Code, "guessing", "Encounter"));
        Assert.Equal("Exploration", (await Read(campaign.Code)).Mode);

        var changed = await Mode(campaign.Code, campaign.DmKey, "Downtime");

        Assert.Equal("Downtime", changed.Mode);
        Assert.Equal("Downtime", (await Read(campaign.Code)).Mode);
        Assert.Equal((campaign.Code, changed), Assert.Single(Broadcaster.Modes));
    }

    // design/005 warns by name that two models for one effect would be two paths through the
    // stacking rule. One application reaching two characters is therefore one row, and this
    // asserts the row count as well as the two sheets, because two sheets alone would pass for
    // an implementation that wrote a row each.
    [Fact]
    public async Task OneApplicationReachesTwoCharactersAndIsStoredOnce()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, Fixture("gnibbo.json"));

        var second = JsonNode.Parse(Fixture("gnibbo.json"))!;
        second["build"]!["name"] = "Tarrow";
        var fighter = await Import(campaign.Code, second.ToJsonString());

        var application = Guid.NewGuid();
        await using (var db = database.NewContext())
        {
            await new ApplyEffectHandler(db, Undo, Broadcaster).Handle(
                new ApplyEffect(campaign.Code, campaign.DmKey, application,
                    Custom("Rallying Anthem", "Status", 1, "Will"),
                    [
                        new EffectTargetSpec("Character", bard.Id),
                        new EffectTargetSpec("Character", fighter.Id),
                    ]),
                default);
        }

        var read = await Read(campaign.Code);
        Assert.All(read.Characters, character =>
            Assert.Equal(application, Assert.Single(character.Effects).Id));

        await using var check = database.NewContext();
        var row = Assert.Single(check.EffectApplications.Where(e => e.Id == application));
        var reached = check.EffectTargets.Where(t => t.ApplicationId == row.Id)
                                         .Select(t => t.TargetId)
                                         .ToList();

        Assert.Equal(2, reached.Count);
        Assert.Contains(bard.Id, reached);
        Assert.Contains(fighter.Id, reached);
    }

    // Re-sending an apply with fewer targets means fewer targets, not the union of both
    // attempts, and an application whose last target leaves goes with it.
    [Fact]
    public async Task NarrowingAnApplicationDropsTheTargetsItNoLongerNames()
    {
        var campaign = await NewCampaign();
        var bard = await Import(campaign.Code, Fixture("gnibbo.json"));

        var second = JsonNode.Parse(Fixture("gnibbo.json"))!;
        second["build"]!["name"] = "Tarrow";
        var fighter = await Import(campaign.Code, second.ToJsonString());

        var application = Guid.NewGuid();
        await Apply(campaign, application, [bard.Id, fighter.Id]);
        await Apply(campaign, application, [bard.Id]);

        var read = await Read(campaign.Code);

        Assert.Single(read.Characters.Single(c => c.Id == bard.Id).Effects);
        Assert.Empty(read.Characters.Single(c => c.Id == fighter.Id).Effects);
    }

    async Task Apply(CreatedCampaignView campaign, Guid application, IEnumerable<Guid> targets)
    {
        await using var db = database.NewContext();
        await new ApplyEffectHandler(db, Undo, Broadcaster).Handle(
            new ApplyEffect(campaign.Code, campaign.DmKey, application,
                Custom("Rallying Anthem", "Status", 1, "Will"),
                [.. targets.Select(id => new EffectTargetSpec("Character", id))]),
            default);
    }

    [Fact]
    public async Task ARuleRecordStatingModifiersIsReadThroughTheSummaryAndTheDetail()
    {
        await using var db = database.NewContext();

        // Every seeded record answers empty today, so the read path the owner's ingest work
        // fills is proved against a record carrying the shape that work will emit.
        db.RuleRecords.Add(new RuleRecord
        {
            Id = "spell-courageous-anthem-test",
            Category = "spell-with-modifiers",
            Name = "Courageous Anthem",
            SourceUrl = "https://2e.aonprd.com/Spells.aspx?ID=1",
            RulesetVersion = "test",
            Mechanics = """
                {"modifiers":[{"type":"Status","value":1,
                  "applies":[{"kind":"Exactly","stat":"Attack"},{"kind":"Exactly","stat":"Damage"}]}]}
                """,
        });
        await db.SaveChangesAsync();

        var found = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "spell-with-modifiers"), default);
        var detail = await new GetRuleHandler(db).Handle(new GetRule("spell-courageous-anthem-test"), default);

        Assert.True(Assert.Single(found.Items).HasModifiers);

        var modifier = Assert.Single(detail!.Modifiers);
        Assert.Equal("Status", modifier.Type);
        Assert.Equal(1, modifier.Value);
        Assert.Equal(["Attack", "Damage"], modifier.Applies.Select(a => a.Stat));
    }

    [Fact]
    public async Task ASeededRecordStatesNoModifierThisAppCanApplyYet()
    {
        await using var db = database.NewContext();

        var conditions = await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: "condition", PageSize: 200), default);

        Assert.NotEmpty(conditions.Items);
        Assert.All(conditions.Items, item => Assert.False(item.HasModifiers));
    }

    // design/004 makes the hit-point operation a delta precisely so two people applying damage
    // at the same moment sum instead of clobbering each other. A delta on the wire is necessary
    // and not sufficient: a handler that reads, adds and writes back puts last-write-wins one
    // layer lower, where the sequential test above cannot see it.
    [Fact]
    public async Task ConcurrentHitPointChangesAllLandRatherThanClobberingEachOther()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));
        const int blows = 20;

        await Task.WhenAll(Enumerable.Range(0, blows)
            .Select(_ => Task.Run(() => Damage(campaign.Code, character.Id, -1))));

        Assert.Equal(
            character.CurrentHitPoints - blows,
            Assert.Single((await Read(campaign.Code)).Characters).CurrentHitPoints);
    }

    // Doing the arithmetic in the database leaves the instance the handler holds stale, and a
    // re-query does not fix it, because identity resolution hands back the object already
    // tracked. Reading the campaign afterwards cannot catch that: the row is right and only the
    // answer is wrong, so this asserts the answer against the row.
    [Fact]
    public async Task ACommandAnswersWithTheNumberTheDatabaseEndsUpHolding()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));

        var answered = await Damage(campaign.Code, character.Id, -9);

        Assert.Equal(
            Assert.Single((await Read(campaign.Code)).Characters).CurrentHitPoints,
            answered!.CurrentHitPoints);
    }

    // The slot id is client-generated so a retry over a flaky connection converges instead of
    // stacking duplicates. A retry is exactly what arrives twice at once, so two requests can
    // both find the slot empty and both insert the same key.
    [Fact]
    public async Task ConcurrentAppliesToOneSlotAllSucceedAndLeaveOneEffect()
    {
        var campaign = await NewCampaign();
        var character = await Import(campaign.Code, Fixture("gnibbo.json"));
        var slot = Guid.NewGuid();
        var spec = new EffectSpec("Clumsy", "Seeded", "clumsy", 2, null, []);

        var answers = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => Set(campaign.Code, character.Id, slot, spec))));

        Assert.All(answers, answer => Assert.Single(answer!.Effects));
        Assert.Single(Assert.Single((await Read(campaign.Code)).Characters).Effects);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingExportIsRejectedByNameRatherThanThrowing(string? payload)
    {
        // Without Cascade.Stop the length rule dereferences the null the previous rule just
        // rejected, and the caller gets a 500 naming nothing instead of a 400 naming the field.
        var result = new ImportCharacterValidator().Validate(new ImportCharacter("ABCDEF", payload!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Pathbuilder");
    }
}
