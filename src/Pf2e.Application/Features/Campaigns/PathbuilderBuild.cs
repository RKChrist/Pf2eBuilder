using System.Collections.Immutable;
using System.Text.Json;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

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

    // The sixteen skills the export states a rank for, keyed by the field it writes them under.
    // A lore is not here because the export puts those in a list of its own with its own shape.
    static readonly (string Field, string Name)[] SkillFields =
    [
        ("acrobatics", "Acrobatics"), ("arcana", "Arcana"), ("athletics", "Athletics"),
        ("crafting", "Crafting"), ("deception", "Deception"), ("diplomacy", "Diplomacy"),
        ("intimidation", "Intimidation"), ("medicine", "Medicine"), ("nature", "Nature"),
        ("occultism", "Occultism"), ("performance", "Performance"), ("religion", "Religion"),
        ("society", "Society"), ("stealth", "Stealth"), ("survival", "Survival"),
        ("thievery", "Thievery"),
    ];

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
                Skills: ReadSkills(proficiencies, build),
                Weapons: ReadWeapons(build),
                Spellcasting: ReadSpellcasting(build),
                Feats: ReadFeats(build),
                Spells: ReadSpells(build),
                AncestryHitPoints: Int(attributes, "ancestryhp", 0),
                ClassHitPoints: Int(attributes, "classhp", 0),
                BonusHitPoints: Int(attributes, "bonushp", 0),
                BonusHitPointsPerLevel: Int(attributes, "bonushpPerLevel", 0));

            return new PathbuilderBuild(character, Int(armor, "pot", 0));
        }
    }

    /// <summary>The sixteen named skills, then every Lore the character wrote down. A lore is
    /// stored as ["Warfare", 2], a name and a rank bonus, and reads at the table as "Warfare
    /// Lore".</summary>
    static ImmutableArray<SkillProficiency> ReadSkills(JsonElement? proficiencies, JsonElement? build)
    {
        var skills = SkillFields
            .Select(field => new SkillProficiency(field.Name, Rank(proficiencies, field.Field)))
            .ToList();

        if (build is { } parent && parent.TryGetProperty("lores", out var lores) && lores.ValueKind is JsonValueKind.Array)
        {
            foreach (var lore in lores.EnumerateArray())
            {
                if (lore.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                var pair = lore.EnumerateArray().ToList();
                if (pair.Count < 2 || pair[0].ValueKind is not JsonValueKind.String || !pair[1].TryGetInt32(out var bonus))
                {
                    continue;
                }

                var name = pair[0].GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    skills.Add(new SkillProficiency(
                        name.EndsWith(" Lore", StringComparison.OrdinalIgnoreCase) ? name : $"{name} Lore",
                        RankByBonus.GetValueOrDefault(bonus, ProficiencyRank.Untrained)));
                }
            }
        }

        return [.. skills];
    }

    /// <summary>
    /// The export states each weapon's finished attack bonus, and that is what is taken. A class
    /// grants proficiency in weapons it names, which no field of the export states: this bard is
    /// untrained in martial weapons and expert with a rapier, so recomputing gives +4 where the
    /// export says +15. The governing attribute is left at Strength here and settled at import
    /// against the seeded weapon's traits, which are the only thing that knows about finesse.
    /// </summary>
    static ImmutableArray<WeaponAttack> ReadWeapons(JsonElement? build)
    {
        if (build is not { } parent
            || !parent.TryGetProperty("weapons", out var weapons)
            || weapons.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. weapons.EnumerateArray()
                .Where(w => w.ValueKind is JsonValueKind.Object)
                .Select(w => (Name: String(w, "name"), Display: String(w, "display"), Bonus: Int(w, "attack", 0)))
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => new WeaponAttack(
                    w.Name!,
                    string.IsNullOrWhiteSpace(w.Display) ? w.Name! : w.Display!,
                    w.Bonus,
                    AttributeKind.Strength)),
        ];
    }

    /// <summary>
    /// The export writes a feat as ["Bardic Lore", null, "Class Feat", 1]: a name, a choice made
    /// inside it, its kind and the level it was taken at. The second slot is the feat's own
    /// option and is not a feat, so it is left alone.
    /// </summary>
    static ImmutableArray<SheetEntry> ReadFeats(JsonElement? build)
    {
        if (build is not { } parent
            || !parent.TryGetProperty("feats", out var feats)
            || feats.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var taken = new List<SheetEntry>();
        foreach (var feat in feats.EnumerateArray().Where(f => f.ValueKind is JsonValueKind.Array))
        {
            var parts = feat.EnumerateArray().ToList();
            if (parts.Count == 0 || parts[0].ValueKind is not JsonValueKind.String)
            {
                continue;
            }

            var name = parts[0].GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var kind = parts.Count > 2 && parts[2].ValueKind is JsonValueKind.String
                ? parts[2].GetString()!
                : "Feat";
            var level = parts.Count > 3 && parts[3].TryGetInt32(out var taken1) ? taken1 : 1;

            taken.Add(new SheetEntry(name, kind, level, null));
        }

        return [.. taken];
    }

    /// <summary>Every spell in every caster block, by rank. A dual-class character has two
    /// blocks and both of their repertoires are theirs, so unlike the spell attack this does not
    /// stop at the first.</summary>
    static ImmutableArray<SheetEntry> ReadSpells(JsonElement? build)
    {
        if (build is not { } parent
            || !parent.TryGetProperty("spellCasters", out var casters)
            || casters.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var known = new List<SheetEntry>();
        foreach (var caster in casters.EnumerateArray().Where(c => c.ValueKind is JsonValueKind.Object))
        {
            if (!caster.TryGetProperty("spells", out var ranks) || ranks.ValueKind is not JsonValueKind.Array)
            {
                continue;
            }

            foreach (var rank in ranks.EnumerateArray().Where(r => r.ValueKind is JsonValueKind.Object))
            {
                var level = Int(rank, "spellLevel", 0);
                if (!rank.TryGetProperty("list", out var list) || list.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var spell in list.EnumerateArray().Where(s => s.ValueKind is JsonValueKind.String))
                {
                    var name = spell.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        known.Add(new SheetEntry(name, level == 0 ? "Cantrip" : $"Rank {level}", level, null));
                    }
                }
            }
        }

        // The same cantrip can be in two blocks, and one row per copy would read as two spells.
        return [.. known.DistinctBy(entry => (entry.Name, entry.Level))];
    }

    /// <summary>The first caster block. A dual-class character can hold two and this shows the
    /// first, which is a limit worth naming rather than a bug worth hiding.</summary>
    static Spellcasting? ReadSpellcasting(JsonElement? build)
    {
        if (build is not { } parent
            || !parent.TryGetProperty("spellCasters", out var casters)
            || casters.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var caster in casters.EnumerateArray().Where(c => c.ValueKind is JsonValueKind.Object))
        {
            var rank = Rank(caster, "proficiency");
            if (rank is ProficiencyRank.Untrained)
            {
                continue;
            }

            var tradition = String(caster, "magicTradition");
            return new Spellcasting(
                string.IsNullOrWhiteSpace(tradition) ? "Spell" : Capitalised(tradition),
                rank,
                AttributeByCode.GetValueOrDefault(String(caster, "ability") ?? string.Empty));
        }

        return null;
    }

    static string Capitalised(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    static ProficiencyRank Rank(JsonElement caster, string name) => Rank((JsonElement?)caster, name);

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
