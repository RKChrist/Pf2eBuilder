using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

public sealed record SelectorChoice(string Token, string Label, SelectorSpecView Spec);

/// <summary>
/// What a custom effect can apply to, as one table. The dropdown and the request are both built
/// from this list, so a new selector is a new row here and nothing else. Nothing anywhere else
/// reads the token apart.
/// </summary>
public static class EffectVocabulary
{
    public static readonly IReadOnlyList<SelectorChoice> All =
    [
        Exactly("ArmorClass", "Armor Class"),
        Exactly("Fortitude", "Fortitude"),
        Exactly("Reflex", "Reflex"),
        Exactly("Will", "Will"),
        Exactly("Perception", "Perception"),
        Exactly("ClassDc", "Class DC"),
        Governed("Strength"),
        Governed("Dexterity"),
        Governed("Constitution"),
        Governed("Intelligence"),
        Governed("Wisdom"),
        Governed("Charisma"),
        new("checks-and-dcs", "All checks and DCs", new SelectorSpecView("AllChecksAndDcs", null, null, null)),
        new("saving-throws", "Saving throws", new SelectorSpecView("SavingThrows", null, null, null)),

        // A skill selector with no skill name is how the engine writes "every skill".
        new("skills", "Skills", new SelectorSpecView("Exactly", "Skill", null, null)),
        new("speed", "Speed", new SelectorSpecView("Speeds", null, null, null)),
    ];

    public static SelectorChoice Of(string token) =>
        All.FirstOrDefault(choice => choice.Token == token) ?? All[0];

    static SelectorChoice Exactly(string stat, string label) =>
        new($"stat:{stat}", label, new SelectorSpecView("Exactly", stat, null, null));

    static SelectorChoice Governed(string attribute) =>
        new($"governed:{attribute}",
            $"Every {attribute}-based statistic",
            new SelectorSpecView("Governed", null, attribute, null));
}
