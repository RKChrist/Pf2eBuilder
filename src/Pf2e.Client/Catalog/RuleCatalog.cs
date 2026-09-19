using Pf2e.Client.State;

namespace Pf2e.Client.Catalog;

/// <summary><paramref name="Route"/> is how a category gets a screen of its own without any
/// renderer learning a category name.</summary>
public sealed record RuleCategory(string Key, string Label, GroupKey Group, string Route = RuleCatalog.BrowseRoute);

public static class RuleCatalog
{
    public const string BrowseRoute = "/";

    public const string ConditionsRoute = "/conditions";

    public const int CategoryCount = 74;

    public static IReadOnlyList<RuleCategory> All { get; } =
    [
        new("ancestry", "Ancestries", GroupKey.Build),
        new("heritage", "Heritages", GroupKey.Build),
        new("background", "Backgrounds", GroupKey.Build),
        new("class", "Classes", GroupKey.Build),
        new("class-feature", "Class Features", GroupKey.Build),
        new("apparition", "Apparitions", GroupKey.Build),
        new("arcane-school", "Arcane Schools", GroupKey.Build),
        new("arcane-thesis", "Arcane Theses", GroupKey.Build),
        new("bloodline", "Bloodlines", GroupKey.Build),
        new("cause", "Causes", GroupKey.Build),
        new("conscious-mind", "Conscious Minds", GroupKey.Build),
        new("doctrine", "Doctrines", GroupKey.Build),
        new("druidic-order", "Druidic Orders", GroupKey.Build),
        new("eidolon", "Eidolons", GroupKey.Build),
        new("element", "Elements", GroupKey.Build),
        new("epithet", "Epithets", GroupKey.Build),
        new("fatal-method", "Fatal Methods", GroupKey.Build),
        new("grim-fascination", "Grim Fascinations", GroupKey.Build),
        new("hunters-edge", "Hunter's Edges", GroupKey.Build),
        new("hybrid-study", "Hybrid Studies", GroupKey.Build),
        new("ikon", "Ikons", GroupKey.Build),
        new("implement", "Implements", GroupKey.Build),
        new("innovation", "Innovations", GroupKey.Build),
        new("instinct", "Instincts", GroupKey.Build),
        new("lesson", "Lessons", GroupKey.Build),
        new("methodology", "Methodologies", GroupKey.Build),
        new("muse", "Muses", GroupKey.Build),
        new("mystery", "Mysteries", GroupKey.Build),
        new("patron", "Patrons", GroupKey.Build),
        new("practice", "Practices", GroupKey.Build),
        new("racket", "Rackets", GroupKey.Build),
        new("research-field", "Research Fields", GroupKey.Build),
        new("style", "Styles", GroupKey.Build),
        new("subconscious-mind", "Subconscious Minds", GroupKey.Build),
        new("way", "Ways", GroupKey.Build),

        new("feat", "Feats", GroupKey.Feats),
        new("archetype", "Archetypes", GroupKey.Feats),

        new("spell", "Spells", GroupKey.Spells),
        new("ritual", "Rituals", GroupKey.Spells),
        new("tradition", "Traditions", GroupKey.Spells),

        new("equipment", "Equipment", GroupKey.Gear),
        new("weapon", "Weapons", GroupKey.Gear),
        new("armor", "Armor", GroupKey.Gear),
        new("shield", "Shields", GroupKey.Gear),
        new("item-bonus", "Item Bonuses", GroupKey.Gear),
        new("relic", "Relics", GroupKey.Gear),
        new("set-relic", "Set Relics", GroupKey.Gear),
        new("curse", "Curses", GroupKey.Gear),
        new("class-kit", "Class Kits", GroupKey.Gear),

        new("action", "Actions", GroupKey.Play),
        new("condition", "Conditions", GroupKey.Play, ConditionsRoute),
        new("skill", "Skills", GroupKey.Play),
        new("skill-general-action", "Skill General Actions", GroupKey.Play),

        new("trait", "Traits", GroupKey.Reference),
        new("language", "Languages", GroupKey.Reference),
        new("deity", "Deities", GroupKey.Reference),
        new("deity-category", "Deity Categories", GroupKey.Reference),
        new("domain", "Domains", GroupKey.Reference),
        new("source", "Sources", GroupKey.Reference),
        new("animal-companion", "Animal Companions", GroupKey.Reference),
        new("animal-companion-advanced", "Advanced Animal Companions", GroupKey.Reference),
        new("animal-companion-specialization", "Animal Companion Specializations", GroupKey.Reference),
        new("animal-companion-unique", "Unique Animal Companions", GroupKey.Reference),
        new("armor-group", "Armor Groups", GroupKey.Reference),
        new("deviant-ability-classification", "Deviant Ability Classifications", GroupKey.Reference),
        new("draconic-exemplar", "Draconic Exemplars", GroupKey.Reference),
        new("familiar-ability", "Familiar Abilities", GroupKey.Reference),
        new("familiar-specific", "Specific Familiars", GroupKey.Reference),
        new("follower", "Followers", GroupKey.Reference),
        new("hellknight-order", "Hellknight Orders", GroupKey.Reference),
        new("mythic-calling", "Mythic Callings", GroupKey.Reference),
        new("runesmith-rune", "Runesmith Runes", GroupKey.Reference),
        new("tactic", "Tactics", GroupKey.Reference),
        new("weapon-group", "Weapon Groups", GroupKey.Reference),
    ];

    public static IReadOnlyList<RuleCategory> InGroup(GroupKey group) =>
        [.. All.Where(category => category.Group == group)];

    public static string LabelOf(string key) =>
        All.FirstOrDefault(category => category.Key == key)?.Label ?? key;

    public static void EnsureComplete()
    {
        var distinct = All.Select(category => category.Key).ToHashSet(StringComparer.Ordinal).Count;

        if (All.Count != CategoryCount || distinct != CategoryCount)
        {
            throw new InvalidOperationException(
                $"The rule catalog must hold {CategoryCount} distinct categories, one per seed file. " +
                $"It holds {All.Count} rows with {distinct} distinct keys.");
        }
    }
}
