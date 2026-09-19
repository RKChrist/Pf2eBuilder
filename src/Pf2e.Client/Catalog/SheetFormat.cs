using Pf2e.Components;
using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Catalog;

/// <summary>
/// The small readings every campaign page does of the same values: how a roll is written, what a
/// mode is called, which of a character's skills are trained.
/// <para>Here rather than on a page because five pages read them and a copy on each is five
/// places for one of them to drift. Nothing in this file decides anything; it only says how a
/// value already decided should read.</para>
/// </summary>
public static class SheetFormat
{
    /// <summary>The three modes, and the page each one is on. Short labels, because three of them
    /// share a phone's width.</summary>
    public static IReadOnlyList<(string Mode, string Label, string Route)> Modes { get; } =
    [
        ("Exploration", "Explore", "/campaign/exploration"),
        ("Encounter", "Fight", "/campaign/encounter"),
        ("Downtime", "Downtime", "/campaign/downtime"),
    ];

    public static IReadOnlyList<ChoiceOption<string>> ModeOptions { get; } =
        [.. Modes.Select(mode => new ChoiceOption<string>(mode.Mode, mode.Label))];

    public static string RouteFor(string mode) =>
        Modes.FirstOrDefault(entry => entry.Mode == mode).Route ?? "/campaign";

    /// <summary>What a player reads where the DM gets a switch. A sentence has room to be a word
    /// the switch's labels do not.</summary>
    public static string ModeName(string mode) => mode switch
    {
        "Encounter" => "In a fight",
        "Downtime" => "Downtime",
        _ => "Exploring",
    };

    /// <summary>A roll reads +14 or -1, never 14, because the number is added to a die.</summary>
    public static string Signed(int total) => total >= 0 ? $"+{total}" : total.ToString();

    /// <summary>An effect chip carries its value when it has one: frightened 2, not frightened.</summary>
    public static string Named(ActiveEffectView effect) =>
        effect.HasValue && effect.Value > 0 ? $"{effect.Name} {effect.Value}" : effect.Name;

    public static IReadOnlyList<NamedBreakdownView> Trained(CharacterSheetView character) =>
        [.. character.Skills.Where(skill => skill.Rank != "Untrained")];

    public static IReadOnlyList<NamedBreakdownView> Untrained(CharacterSheetView character) =>
        [.. character.Skills.Where(skill => skill.Rank == "Untrained")];

    /// <summary>The character's own total in a named skill, or zero for one they do not carry.</summary>
    public static int SkillOf(CharacterSheetView character, string skill) =>
        character.Skills.FirstOrDefault(entry => entry.Name == skill)?.Value.Total ?? 0;

    /// <summary>Everyone on the roster who is not already in the fight. Offering somebody twice is
    /// offering to give them two turns.</summary>
    public static IReadOnlyList<CharacterSheetView> Absent(CampaignView table) =>
    [
        .. table.Characters.Where(character =>
            table.Encounter is null
            || !table.Encounter.Combatants.Any(combatant => combatant.Id == character.Id)),
    ];

    /// <summary>A creature row says what a DM choosing one wants to know, which is how hard it
    /// hits back rather than which book it is from.</summary>
    public static string Glance(RuleSummary creature)
    {
        var facts = creature.Highlights
            .Where(fact => fact.Key is "ac" or "hp" or "size")
            .Select(fact => $"{MechanicsDisplay.LabelOf(fact.Key)} {string.Join(", ", fact.Values)}");

        var level = creature.Level is { } value ? $"Level {value}" : null;
        return string.Join(" · ", new[] { level }.Concat(facts).Where(part => part is { Length: > 0 })!);
    }

    /// <summary>The effect an active one came from, so a card can find whether a condition is on.</summary>
    public static ActiveEffectView? Held(CharacterSheetView character, string key) =>
        character.Effects.FirstOrDefault(effect =>
            effect.Kind == "Seeded" && string.Equals(effect.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Null for anything that is not a number a table would apply, which is what leaves
    /// both hit point buttons disabled rather than sending a request the server will refuse.</summary>
    public static int? Typed(string draft) =>
        int.TryParse(draft, out var amount) && amount is > 0 and <= 999 ? amount : null;
}
