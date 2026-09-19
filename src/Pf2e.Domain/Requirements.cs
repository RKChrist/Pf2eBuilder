using System.Text.RegularExpressions;

namespace Pf2e.Domain;

/// <summary>
/// A requirement a record states in words, read back into a rank and the skills that satisfy it.
/// <para>Only the plain proficiency phrases parse: "trained in Stealth", "expert in Perception",
/// "trained in Arcana, Occultism, Religion, or Society". Everything else stays a sentence. A
/// requirement like "The companion to be learned from must be camping with you" is not something
/// a sheet can check, and pretending otherwise would put a green tick beside a thing the table
/// has to decide.</para>
/// </summary>
public static partial class Requirements
{
    [GeneratedRegex(
        @"^\s*(?<rank>trained|expert|master|legendary)\s+in\s+(?<skills>[A-Za-z' ]+(?:\s*,\s*[A-Za-z' ]+)*(?:\s*,?\s*or\s+[A-Za-z' ]+)?)\s*\.?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Phrase();

    /// <summary>
    /// The rank and the skills, or null when the sentence is not one of these.
    /// <para>A skill the engine does not govern is kept anyway: "trained in Cooking Lore" names a
    /// Lore, and a character who wrote that Lore down has it.</para>
    /// </summary>
    public static (ProficiencyRank Rank, IReadOnlyList<string> Skills)? Parse(string? requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return null;
        }

        var match = Phrase().Match(requirement);
        if (!match.Success)
        {
            return null;
        }

        var rank = match.Groups["rank"].Value.ToLowerInvariant() switch
        {
            "trained" => ProficiencyRank.Trained,
            "expert" => ProficiencyRank.Expert,
            "master" => ProficiencyRank.Master,
            _ => ProficiencyRank.Legendary,
        };

        // "A, B, or C" splits on the comma into "or C", so the conjunction is stripped after the
        // split as well as split on. Doing only one of the two left a skill called "or C".
        var skills = match.Groups["skills"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(part => part.Split(" or ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(skill => skill.Trim())
            .Select(skill => skill.StartsWith("or ", StringComparison.OrdinalIgnoreCase)
                ? skill["or ".Length..].Trim()
                : skill)
            .Where(skill => skill.Length > 0)
            .ToList();

        return skills.Count == 0 ? null : (rank, skills);
    }

    /// <summary>
    /// Whether a character meets a parsed requirement, in any one of the skills it names.
    /// Perception is not a skill and "expert in Perception" is a requirement, so the lookup is
    /// given the name and decides for itself.
    /// </summary>
    public static bool MetBy(
        (ProficiencyRank Rank, IReadOnlyList<string> Skills) requirement,
        Func<string, ProficiencyRank> rankOf) =>
        requirement.Skills.Any(skill => rankOf(skill) >= requirement.Rank);
}
