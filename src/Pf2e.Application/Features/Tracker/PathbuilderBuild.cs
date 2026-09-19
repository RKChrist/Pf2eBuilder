using System.Text.Json;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Tracker;

/// <summary>A payload that is not a Pathbuilder export at all. The API turns it into a 400,
/// which is why it is public.</summary>
public sealed class PathbuilderFormatException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// What the export states. The armour's item bonus and Dex cap are not here because the export
/// does not state them; the seeded ruleset does, and the importer looks them up.
/// </summary>
internal sealed record PathbuilderBuild(Character Build, int ArmorPotency)
{
    const string PasteAdvice =
        "Paste the JSON from Pathbuilder's Export JSON, or open " +
        "https://pathbuilder2e.com/json.php?id=NNNNNN and copy the page.";

    // The export has no version field and two stored records differ by six top-level keys, so
    // there is nothing to branch on and every read has to answer "may be absent, may be the
    // wrong type" on its own.
    static readonly Dictionary<int, ProficiencyRank> RankByBonus = new()
    {
        [0] = ProficiencyRank.Untrained,
        [2] = ProficiencyRank.Trained,
        [4] = ProficiencyRank.Expert,
        [6] = ProficiencyRank.Master,
        [8] = ProficiencyRank.Legendary,
    };

    static readonly Dictionary<string, AttributeKind> AttributeByCode =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["str"] = AttributeKind.Strength,
            ["dex"] = AttributeKind.Dexterity,
            ["con"] = AttributeKind.Constitution,
            ["int"] = AttributeKind.Intelligence,
            ["wis"] = AttributeKind.Wisdom,
            ["cha"] = AttributeKind.Charisma,
        };

    static readonly Dictionary<string, string> ArmorRankFieldByCategory =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["unarmored"] = "unarmored",
            ["light"] = "light",
            ["medium"] = "medium",
            ["heavy"] = "heavy",
        };

    public static PathbuilderBuild Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException failure)
        {
            throw new PathbuilderFormatException($"That is not a Pathbuilder export. {PasteAdvice}", failure);
        }

        using (document)
        {
            var root = document.RootElement;
            var build = Object(root, "build") ?? (root.ValueKind is JsonValueKind.Object ? root : null);
            var name = String(build, "name");

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PathbuilderFormatException($"That export carries no character name. {PasteAdvice}");
            }

            var abilities = Object(build, "abilities");
            var proficiencies = Object(build, "proficiencies");
            var attributes = Object(build, "attributes");
            var armor = WornArmor(build);
            var armorCategory = String(armor, "prof") ?? "unarmored";

            var character = new Character(
                Name: name,
                Level: Math.Clamp(Int(build, "level", 1), 1, 30),
                ClassName: String(build, "class") ?? "Unknown class",
                AncestryName: String(build, "ancestry") ?? "Unknown ancestry",
                KeyAttribute: AttributeByCode.GetValueOrDefault(String(build, "keyability") ?? string.Empty),
                Attributes: new AttributeModifiers(
                    Strength: Score(abilities, "str"),
                    Dexterity: Score(abilities, "dex"),
                    Constitution: Score(abilities, "con"),
                    Intelligence: Score(abilities, "int"),
                    Wisdom: Score(abilities, "wis"),
                    Charisma: Score(abilities, "cha")),
                Fortitude: Rank(proficiencies, "fortitude"),
                Reflex: Rank(proficiencies, "reflex"),
                Will: Rank(proficiencies, "will"),
                Perception: Rank(proficiencies, "perception"),
                ClassDc: Rank(proficiencies, "classDC"),
                ArmorRank: Rank(proficiencies, ArmorRankFieldByCategory.GetValueOrDefault(armorCategory, "unarmored")),
                ArmorName: String(armor, "name") ?? "Unarmored",
                ArmorItemBonus: 0,
                ArmorDexCap: null,
                AncestryHitPoints: Int(attributes, "ancestryhp", 0),
                ClassHitPoints: Int(attributes, "classhp", 0),
                BonusHitPoints: Int(attributes, "bonushp", 0),
                BonusHitPointsPerLevel: Int(attributes, "bonushpPerLevel", 0));

            return new PathbuilderBuild(character, Int(armor, "pot", 0));
        }
    }

    static JsonElement? WornArmor(JsonElement? build)
    {
        if (build is not { } parent
            || !parent.TryGetProperty("armor", out var armor)
            || armor.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        var entries = armor.EnumerateArray().Where(e => e.ValueKind is JsonValueKind.Object).ToList();
        return entries.FirstOrDefault(e => Bool(e, "worn")) is { ValueKind: JsonValueKind.Object } worn
            ? worn
            : entries.Count > 0 ? entries[0] : null;
    }

    /// <summary>The export states final scores, not modifiers, and a missing one reads as 10.</summary>
    static int Score(JsonElement? parent, string name) => Ability.Modifier(Int(parent, name, 10));

    /// <summary>The export states rank bonuses of 0, 2, 4, 6 or 8 rather than ranks, and
    /// <see cref="ProficiencyRank"/>'s members are literally those numbers.</summary>
    static ProficiencyRank Rank(JsonElement? parent, string name) =>
        RankByBonus.GetValueOrDefault(Int(parent, name, 0), ProficiencyRank.Untrained);

    static JsonElement? Object(JsonElement? parent, string name) =>
        parent is { } element
        && element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.Object
            ? value
            : null;

    static string? String(JsonElement? parent, string name) =>
        parent is { } element
        && element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    static int Int(JsonElement? parent, string name, int fallback) =>
        parent is { } element
        && element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : fallback;

    static bool Bool(JsonElement? parent, string name) =>
        parent is { } element
        && element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True;
}
