using Pf2e.Components;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.State;

/// <summary>
/// What the editor needs that the form itself does not: the vocabularies its dropdowns offer,
/// and the one conversion a reader should not have to do in their head.
/// <para>The starting values are not computed here. They come down with the character, as
/// <see cref="CharacterSheetView.Build"/>, because a sheet states Fortitude +11 and an edit
/// states Trained, and one cannot be worked back from the other without knowing the level and
/// the attribute it was built from. Sending the build with the sheet is cheaper than guessing.</para>
/// </summary>
public static class CharacterEdits
{
    public static IReadOnlyList<ChoiceOption<string>> Ranks { get; } =
    [
        new("Untrained", "Untrained"),
        new("Trained", "Trained"),
        new("Expert", "Expert"),
        new("Master", "Master"),
        new("Legendary", "Legendary"),
    ];

    public static IReadOnlyList<ChoiceOption<string>> Attributes { get; } =
    [
        new("Strength", "Strength"),
        new("Dexterity", "Dexterity"),
        new("Constitution", "Constitution"),
        new("Intelligence", "Intelligence"),
        new("Wisdom", "Wisdom"),
        new("Charisma", "Charisma"),
    ];

    /// <summary>
    /// A modifier reads +3, never 3, because it is added to a die. The editor works in modifiers
    /// throughout: a player reading their own sheet sees Dexterity +3, and making them type 16 to
    /// mean the same thing is a conversion the app can do and they should not have to.
    /// </summary>
    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
}
