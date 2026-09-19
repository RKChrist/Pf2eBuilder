using Pf2e.Client.State;

namespace Pf2e.Client.Catalog;

/// <summary>Our own words, never Paizo's: the ingest withholds rule prose, and these fill the gap
/// with a plain explanation a new player can act on.</summary>
public sealed record CategoryNote(string Section, string Blurb);

public static class GroupNotes
{
    public static IReadOnlyDictionary<GroupKey, string> All { get; } = new Dictionary<GroupKey, string>
    {
        [GroupKey.Build] = "The choices that make a character: who they are, where they come from, and what they trained as.",
    };

    public static string Of(GroupKey group) => All.GetValueOrDefault(group, string.Empty);
}

public static class CategoryNotes
{
    public static IReadOnlyDictionary<string, CategoryNote> All { get; } =
        new Dictionary<string, CategoryNote>(StringComparer.Ordinal)
        {
            ["ancestry"] = new("Core", "Your people. Sets hit points, size, speed and which heritages and ancestry feats are open to you."),
        };

    public static CategoryNote? Of(string category) => All.GetValueOrDefault(category);
}

/// <summary>Keyed by the trait's name exactly as the seed spells it. A trait with no entry still
/// opens; it shows its facts and where it is used, just no gloss.</summary>
public static class TraitGlossary
{
    public static IReadOnlyDictionary<string, string> All { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Manipulate"] = "You have to move your hands to do this, so it can trigger reactions like Reactive Strike.",
        };

    public static string? Of(string trait) => All.GetValueOrDefault(trait);
}
