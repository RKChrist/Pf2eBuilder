using Pf2e.Client.State;

namespace Pf2e.Client.Catalog;

public sealed record RuleGroup(GroupKey Key, string Label, string Heading);

/// <summary>
/// The bottom bar holds six items across 320px, so the bar label is short and the screen
/// heading is the full word.
/// </summary>
public static class RuleGroups
{
    public static IReadOnlyList<RuleGroup> All { get; } =
    [
        new(GroupKey.Build, "Build", "Build a character"),
        new(GroupKey.Feats, "Feats", "Feats"),
        new(GroupKey.Spells, "Spells", "Spells"),
        new(GroupKey.Gear, "Gear", "Gear"),
        new(GroupKey.Play, "Play", "At the table"),
        new(GroupKey.Reference, "Refs", "Reference"),
    ];

    public static RuleGroup Of(GroupKey key) => All.First(group => group.Key == key);
}
