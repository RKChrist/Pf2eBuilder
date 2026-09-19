using Pf2e.Application.Features.Campaigns;
using Pf2e.Application.Features.Rules;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Infrastructure.Persistence;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The effect picker's third arm searches the whole ruleset and applies what a record states.
/// It applied nothing at all until the ingest started emitting modifiers, because Archives of
/// Nethys publishes a rule's numbers in prose and this project does not import prose. Where it
/// publishes a number as a field, which is an item's bonus to a skill, the ingest now extracts
/// it, and these are the records that reach a character because of that.
/// </summary>
public class ExtractedModifiers(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    RecordingBroadcaster Broadcaster { get; } = new();

    async Task<RuleSearchResult> Search(string category, string? name = null, int page = 1)
    {
        await using var db = database.NewContext();
        return await new SearchRulesHandler(db)
            .Handle(new SearchRules(Category: category, Name: name, Page: page, PageSize: 100), default);
    }

    async Task<RuleSummary> Horn()
    {
        var found = await Search("item-bonus", "Horn of Blasting");
        return found.Items[0];
    }

    [Fact]
    public async Task AnItemsBonusToASkillIsAModifierAndNotJustAPriceTag()
    {
        await using var db = database.NewContext();
        var horn = await Horn();

        Assert.True(horn.HasModifiers, "the row would offer nothing to apply");

        var detail = (await new GetRuleHandler(db).Handle(new GetRule(horn.Id), default))!;
        var modifier = Assert.Single(detail.Modifiers);

        Assert.Equal("Item", modifier.Type);
        Assert.Equal(2, modifier.Value);
        var applies = Assert.Single(modifier.Applies);
        Assert.Equal("Exactly", applies.Kind);
        Assert.Equal("Skill", applies.Stat);
        Assert.Equal("Performance", applies.SkillName);
    }

    [Fact]
    public async Task ARecordPickedOutOfTheRulesSearchChangesTheNumberItNames()
    {
        var horn = await Horn();
        RuleDetail detail;
        CreatedCampaignView campaign;
        CharacterSheetView character;

        await using (var db = database.NewContext())
        {
            detail = (await new GetRuleHandler(db).Handle(new GetRule(horn.Id), default))!;
            campaign = await new CreateCampaignHandler(db).Handle(new CreateCampaign(), default);
            character = await new ImportCharacterHandler(db, db, Broadcaster)
                .Handle(new ImportCharacter(campaign.Code, Fixture("gnibbo.json")), default);
        }

        var before = character.Skills.ToDictionary(skill => skill.Name, skill => skill.Value.Total);

        // Exactly what the picker sends when a player taps a row in the rules arm.
        CharacterSheetView? after;
        await using (var db = database.NewContext())
        {
            var applied = await new ApplyEffectHandler(db, new MemoryUndoStack(), Broadcaster).Handle(
                new ApplyEffect(campaign.Code, null, Guid.NewGuid(),
                    new EffectSpec(horn.Name, "Rule", horn.Id, 0, null, detail.Modifiers),
                    [new EffectTargetSpec("Character", character.Id)]),
                default);
            after = applied.Characters.SingleOrDefault(c => c.Id == character.Id);
        }

        Assert.NotNull(after);
        Assert.Equal(before["Performance"] + 2, Total(after, "Performance"));

        // An item bonus to Performance is an item bonus to Performance. Everything else on the
        // sheet is where it was.
        foreach (var (name, total) in before.Where(entry => entry.Key != "Performance"))
        {
            Assert.Equal(total, Total(after, name));
        }

        Assert.Equal(character.ArmorClass.Total, after.ArmorClass.Total);
    }

    [Fact]
    public async Task TheExtractionCoversTheRecordsThatNameASkillAndLeavesTheRestAlone()
    {
        var stating = 0;
        var total = 0;
        for (var page = 1; ; page++)
        {
            var found = await Search("item-bonus", page: page);
            if (found.Items.Count == 0)
            {
                break;
            }

            total += found.Items.Count;
            stating += found.Items.Count(item => item.HasModifiers);
            if (total >= found.TotalMatching)
            {
                break;
            }
        }

        Assert.Equal(987, total);

        // The other 158 qualify a Perception bonus by the action being taken, such as "Perception
        // rolls for initiative", which is a predicate this engine has no way to say. They state
        // nothing here rather than stating something wrong.
        Assert.Equal(829, stating);
    }

    [Fact]
    public async Task ARecordThatStatesNothingSaysSoRatherThanOfferingAnEmptyEffect()
    {
        // A spell's record carries its tradition and its level and not one word of what it does,
        // because the pull does not fetch rule text at all. So bless is a name here, and the
        // engine's own registry is where its +1 lives.
        var found = await Search("spell", "Bless");
        var bless = found.Items.First(rule => rule.Name == "Bless");

        Assert.False(bless.HasModifiers);
        Assert.NotNull(Effects.Find("bless"));
    }

    [Fact]
    public async Task AnItemBonusRowSaysWhatItIsForBeforeAnyoneOpensIt()
    {
        var keys = (await Horn()).Highlights.Select(highlight => highlight.Key).ToList();

        Assert.Contains("item_bonus_value", keys);
        Assert.Contains("skill", keys);
        Assert.Contains("item_bonus_note", keys);
    }

    static int Total(CharacterSheetView character, string skill) =>
        character.Skills.Single(entry => entry.Name == skill).Value.Total;
}
