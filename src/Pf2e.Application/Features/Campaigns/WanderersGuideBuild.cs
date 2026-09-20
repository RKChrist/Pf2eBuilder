using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// A Wanderer's Guide export.
/// <para>A different shape from a Pathbuilder one and, for most of what a sheet needs, a better
/// one: its <c>content</c> section is the builder's own compiled stats, and it says so in its own
/// README. Every proficiency arrives as a rank and a finished total, every attribute as a
/// modifier rather than a score, and the armour carries its AC bonus and Dex cap.</para>
/// <para>The files are large — thirteen megabytes for a level 13 rogue — because the export
/// embeds the full record of every item and spell it touched. Almost none of that is read here:
/// the parts this needs come to about 150 KB, and the rest is walked past.</para>
/// </summary>
/// <param name="ArmorPotency">Kept beside the build rather than folded into it, because the
/// importer looks the armour up in the ruleset afterwards and rewrites its item bonus from the
/// seeded record plus this. Folding it in first meant it was computed twice and the second one
/// won with the rune missing.</param>
internal sealed record WanderersGuideBuild(Character Build, int ArmorPotency)
{
    /// <summary>The three keys the format is known by. Checked before parsing so a Pathbuilder
    /// export is never half-read as this and rejected with the wrong sentence.</summary>
    public static bool Looks(JsonElement root) =>
        root.ValueKind is JsonValueKind.Object
        && root.TryGetProperty("character", out var character)
        && character.ValueKind is JsonValueKind.Object
        && root.TryGetProperty("content", out var content)
        && content.ValueKind is JsonValueKind.Object;

    static readonly Dictionary<int, ProficiencyRank> RankByBonus = new()
    {
        [0] = ProficiencyRank.Untrained,
        [2] = ProficiencyRank.Trained,
        [4] = ProficiencyRank.Expert,
        [6] = ProficiencyRank.Master,
        [8] = ProficiencyRank.Legendary,
    };

    /// <summary>The sixteen the engine governs, by the key the export writes them under. Lores
    /// arrive as SKILL_LORE_&lt;SUBJECT&gt; and are handled separately, because their names are
    /// not a fixed list.</summary>
    static readonly (string Key, string Name)[] SkillKeys =
    [
        ("SKILL_ACROBATICS", "Acrobatics"), ("SKILL_ARCANA", "Arcana"),
        ("SKILL_ATHLETICS", "Athletics"), ("SKILL_CRAFTING", "Crafting"),
        ("SKILL_DECEPTION", "Deception"), ("SKILL_DIPLOMACY", "Diplomacy"),
        ("SKILL_INTIMIDATION", "Intimidation"), ("SKILL_MEDICINE", "Medicine"),
        ("SKILL_NATURE", "Nature"), ("SKILL_OCCULTISM", "Occultism"),
        ("SKILL_PERFORMANCE", "Performance"), ("SKILL_RELIGION", "Religion"),
        ("SKILL_SOCIETY", "Society"), ("SKILL_STEALTH", "Stealth"),
        ("SKILL_SURVIVAL", "Survival"), ("SKILL_THIEVERY", "Thievery"),
    ];

    static readonly Dictionary<string, AttributeKind> AttributeByKey = new(StringComparer.Ordinal)
    {
        ["ATTRIBUTE_STR"] = AttributeKind.Strength,
        ["ATTRIBUTE_DEX"] = AttributeKind.Dexterity,
        ["ATTRIBUTE_CON"] = AttributeKind.Constitution,
        ["ATTRIBUTE_INT"] = AttributeKind.Intelligence,
        ["ATTRIBUTE_WIS"] = AttributeKind.Wisdom,
        ["ATTRIBUTE_CHA"] = AttributeKind.Charisma,
    };

    /// <summary>Which armour proficiency the worn armour is governed by, from its own category.</summary>
    static readonly Dictionary<string, string> ArmorKeyByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unarmored"] = "UNARMORED_DEFENSE",
        ["light"] = "LIGHT_ARMOR",
        ["medium"] = "MEDIUM_ARMOR",
        ["heavy"] = "HEAVY_ARMOR",
    };

    public static WanderersGuideBuild Parse(JsonElement root)
    {
        var character = root.GetProperty("character");
        var content = root.GetProperty("content");

        var name = String(character, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PathbuilderFormatException(
                "That Wanderer's Guide export carries no character name.");
        }

        var details = Object(character, "details");
        var attributes = Object(content, "attributes");
        var proficiencies = Object(content, "proficiencies");
        var level = Math.Clamp(Int(character, "level", 1), 1, 30);
        var armor = Object(content, "armor_item");
        var armorMeta = Object(Object(armor, "item"), "meta_data");

        var build = new Character(
            Name: name!.Trim(),
            Level: level,
            ClassName: String(Object(details, "class"), "name") ?? "Unknown class",
            AncestryName: String(Object(details, "ancestry"), "name") ?? "Unknown ancestry",
            KeyAttribute: KeyAttribute(proficiencies, attributes),
            Attributes: new AttributeModifiers(
                Strength: Modifier(attributes, "ATTRIBUTE_STR"),
                Dexterity: Modifier(attributes, "ATTRIBUTE_DEX"),
                Constitution: Modifier(attributes, "ATTRIBUTE_CON"),
                Intelligence: Modifier(attributes, "ATTRIBUTE_INT"),
                Wisdom: Modifier(attributes, "ATTRIBUTE_WIS"),
                Charisma: Modifier(attributes, "ATTRIBUTE_CHA")),
            Fortitude: Rank(proficiencies, "SAVE_FORT"),
            Reflex: Rank(proficiencies, "SAVE_REFLEX"),
            Will: Rank(proficiencies, "SAVE_WILL"),
            Perception: Rank(proficiencies, "PERCEPTION"),
            ClassDc: Rank(proficiencies, "CLASS_DC"),
            ArmorRank: Rank(proficiencies, ArmorKeyByCategory.GetValueOrDefault(
                String(armorMeta, "category") ?? "unarmored", "UNARMORED_DEFENSE")),
            ArmorName: String(Object(armor, "item"), "name") ?? "Unarmored",
            // Overwritten at import from the seeded armour record plus the potency below, the
            // same way a Pathbuilder import is.
            ArmorItemBonus: Int(armorMeta, "ac_bonus", 0),
            ArmorDexCap: armorMeta is { } cap && cap.TryGetProperty("dex_cap", out var dex)
                         && dex.ValueKind is JsonValueKind.Number
                ? dex.GetInt32()
                : null,
            AncestryHitPoints: 0,
            ClassHitPoints: 0,
            BonusHitPoints: 0,
            BonusHitPointsPerLevel: 0,
            Skills: ReadSkills(proficiencies),
            Weapons: ReadWeapons(content),
            Spellcasting: ReadSpellcasting(proficiencies, attributes),
            Feats: ReadFeats(content),
            Spells: ReadSpells(content))
        {
            // The export states the finished total, which is the only form it comes in: there is
            // no ancestry-and-class split to read. Taking it whole is the same call the weapon
            // bonuses take, and for the same reason.
            StatedMaxHitPoints = Int(content, "max_hp", 0) is var stated and > 0 ? stated : null,
        };

        return new WanderersGuideBuild(build, Potency(armorMeta));
    }

    /// <summary>The character object on its own, which is what a Wanderer's Guide link returns and
    /// what an export holds under <c>character</c>. Also accepted wrapped the way their API sends
    /// it, because that is what somebody copying the response would paste.</summary>
    public static bool LooksShared(JsonElement root) => Shared(root) is not null;

    static JsonElement? Shared(JsonElement root)
    {
        var character = Object(root, "data") ?? root;
        return character.ValueKind is JsonValueKind.Object
               && Object(character, "meta_data") is not null
               && Object(character, "details") is not null
               && String(character, "name") is not null
            ? character
            : null;
    }

    static readonly Dictionary<string, ProficiencyRank> RankByLetter = new(StringComparer.Ordinal)
    {
        ["U"] = ProficiencyRank.Untrained,
        ["T"] = ProficiencyRank.Trained,
        ["E"] = ProficiencyRank.Expert,
        ["M"] = ProficiencyRank.Master,
        ["L"] = ProficiencyRank.Legendary,
    };

    /// <summary>
    /// A character from the numbers Wanderer's Guide keeps beside it for its own campaign view:
    /// maximum hit points, armour class, and a finished total and a rank for every save, skill,
    /// perception and DC.
    /// <para>There are no attributes, no armour and no weapons in it, because those only exist
    /// once their rules engine has run, and that happens in a browser. So every number here is
    /// stated rather than computed, the attributes stay at zero, and the sheet has no attacks,
    /// feats or spells. The export file is how those arrive.</para>
    /// <para>Their DC totals leave the ten out and ours carry it.</para>
    /// </summary>
    public static WanderersGuideBuild ParseShared(JsonElement root)
    {
        var character = Shared(root)
            ?? throw new PathbuilderFormatException("That is not a Wanderer's Guide character.");

        var name = String(character, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PathbuilderFormatException("That Wanderer's Guide character has no name.");
        }

        var stats = Object(Object(character, "meta_data"), "calculated_stats");
        var profs = Object(stats, "profs");
        if (stats is null || profs is null || Int(stats, "hp_max", 0) <= 0)
        {
            throw new PathbuilderFormatException(
                "Wanderer's Guide has not worked this character's numbers out yet. Open the character " +
                "there once so it does, or export the character to a file and paste that instead.");
        }

        ProficiencyRank Letter(string key) =>
            RankByLetter.GetValueOrDefault(String(Object(profs, key), "type") ?? "U", ProficiencyRank.Untrained);

        int? Total(string key) =>
            Object(profs, key) is { } entry && entry.TryGetProperty("total", out var total)
            && total.ValueKind is JsonValueKind.Number && total.TryGetInt32(out var value)
                ? value
                : null;

        var skills = new List<SkillProficiency>();
        var totals = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.OrdinalIgnoreCase);
        void Skill(string key, string shown)
        {
            skills.Add(new SkillProficiency(shown, Letter(key)));
            if (Total(key) is { } total)
            {
                totals[shown] = total;
            }
        }

        foreach (var (key, shown) in SkillKeys)
        {
            Skill(key, shown);
        }

        foreach (var entry in profs.Value.EnumerateObject())
        {
            if (entry.Name.StartsWith("SKILL_LORE_", StringComparison.Ordinal)
                && Titled(entry.Name["SKILL_LORE_".Length..]) is { Length: > 0 } subject)
            {
                Skill(entry.Name, $"{subject} Lore");
            }
        }

        var details = Object(character, "details");
        var casts = Letter("SPELL_ATTACK") is not ProficiencyRank.Untrained;

        var build = new Character(
            Name: name!.Trim(),
            Level: Math.Clamp(Int(character, "level", 1), 1, 30),
            ClassName: String(Object(details, "class"), "name") ?? "Unknown class",
            AncestryName: String(Object(details, "ancestry"), "name") ?? "Unknown ancestry",
            // Not stated anywhere in what a link returns. It only decides which conditions reach
            // the class DC, and the DC itself is stated.
            KeyAttribute: AttributeKind.Strength,
            Attributes: new AttributeModifiers(0, 0, 0, 0, 0, 0),
            Fortitude: Letter("SAVE_FORT"),
            Reflex: Letter("SAVE_REFLEX"),
            Will: Letter("SAVE_WILL"),
            Perception: Letter("PERCEPTION"),
            ClassDc: Letter("CLASS_DC"),
            ArmorRank: ProficiencyRank.Untrained,
            ArmorName: "Unarmored",
            ArmorItemBonus: 0,
            ArmorDexCap: null,
            AncestryHitPoints: 0,
            ClassHitPoints: 0,
            BonusHitPoints: 0,
            BonusHitPointsPerLevel: 0,
            Skills: [.. skills],
            Spellcasting: casts ? new Spellcasting("Spell", Letter("SPELL_ATTACK"), AttributeKind.Charisma) : null)
        {
            StatedMaxHitPoints = Int(stats, "hp_max", 0),
            Stated = new StatedTotals(
                ArmorClass: Int(stats, "ac", 0) is var ac and > 0 ? ac : null,
                Fortitude: Total("SAVE_FORT"),
                Reflex: Total("SAVE_REFLEX"),
                Will: Total("SAVE_WILL"),
                Perception: Total("PERCEPTION"),
                ClassDc: Total("CLASS_DC") is { } classDc ? 10 + classDc : null,
                SpellAttack: casts ? Total("SPELL_ATTACK") : null,
                SpellDc: casts && Total("SPELL_DC") is { } spellDc ? 10 + spellDc : null,
                Skills: totals.ToImmutable()),
        };

        return new WanderersGuideBuild(build, ArmorPotency: 0);
    }

    /// <summary>
    /// The attribute the class DC is built from, worked back from the numbers rather than read:
    /// the export states no key attribute, and the class DC is level plus rank plus exactly it.
    /// A tie or no match falls back to the highest attribute, which is the same guess a player
    /// would make.
    /// </summary>
    static AttributeKind KeyAttribute(JsonElement? proficiencies, JsonElement? attributes)
    {
        var classDc = Part(proficiencies, "CLASS_DC", "attributeMod");
        var byValue = AttributeByKey
            .Select(pair => (pair.Value, Modifier(attributes, pair.Key)))
            .ToList();

        var matches = byValue.Where(pair => pair.Item2 == classDc).ToList();
        return matches.Count == 1
            ? matches[0].Value
            : byValue.OrderByDescending(pair => pair.Item2).First().Value;
    }

    /// <summary>Every skill the export carries, including the Lores, which arrive one key each
    /// and are named by what follows the prefix.</summary>
    static ImmutableArray<SkillProficiency> ReadSkills(JsonElement? proficiencies)
    {
        var skills = SkillKeys
            .Select(skill => new SkillProficiency(skill.Name, Rank(proficiencies, skill.Key)))
            .ToList();

        if (proficiencies is not { } all)
        {
            return [.. skills];
        }

        foreach (var entry in all.EnumerateObject())
        {
            if (!entry.Name.StartsWith("SKILL_LORE_", StringComparison.Ordinal))
            {
                continue;
            }

            // SKILL_LORE____ is the export's empty slot, not a lore called nothing.
            var subject = Titled(entry.Name["SKILL_LORE_".Length..]);
            if (subject.Length == 0)
            {
                continue;
            }

            skills.Add(new SkillProficiency($"{subject} Lore", Rank(proficiencies, entry.Name)));
        }

        return [.. skills];
    }

    /// <summary>UNDERWORLD becomes Underworld, and TWO_WORDS becomes Two Words.
    /// <para>Lowercased first, because ToTitleCase leaves an already-uppercase word alone: it
    /// is written to capitalise "underworld" and to leave "NASA" as an acronym, and every word
    /// in this export is shouted.</para></summary>
    static string Titled(string shouted) =>
        string.Join(
            ' ',
            shouted.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant())));

    /// <summary>
    /// The export states each weapon's finished attack bonus, three of them for the three strikes
    /// of a turn, and the first is the one a sheet shows. Which attribute governs it is settled at
    /// import against the seeded weapon's traits, exactly as the Pathbuilder path does.
    /// </summary>
    static ImmutableArray<WeaponAttack> ReadWeapons(JsonElement content)
    {
        if (!content.TryGetProperty("weapons", out var weapons)
            || weapons.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var carried = new List<WeaponAttack>();
        foreach (var weapon in weapons.EnumerateArray().Where(w => w.ValueKind is JsonValueKind.Object))
        {
            var item = Object(weapon, "item");
            var name = String(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var bonus = Object(Object(weapon, "stats"), "attack_bonus");
            var first = bonus is { } b && b.TryGetProperty("total", out var totals)
                        && totals.ValueKind is JsonValueKind.Array && totals.GetArrayLength() > 0
                        && totals[0].ValueKind is JsonValueKind.Number
                ? totals[0].GetInt32()
                : 0;

            // A specific magic weapon is named for itself, so the base item is what the ruleset
            // can be asked about: Hunter's Anthem is a shortbow, and Finesse is a shortbow's to say.
            var baseItem = String(Object(item, "meta_data"), "base_item");
            carried.Add(new WeaponAttack(
                Titled((baseItem ?? name!).Replace('-', '_')),
                name!,
                first,
                AttributeKind.Strength));
        }

        return [.. carried];
    }

    static Spellcasting? ReadSpellcasting(JsonElement? proficiencies, JsonElement? attributes)
    {
        var rank = Rank(proficiencies, "SPELL_ATTACK");
        if (rank is ProficiencyRank.Untrained)
        {
            return null;
        }

        var modifier = Part(proficiencies, "SPELL_ATTACK", "attributeMod");
        var attribute = AttributeByKey
            .Select(pair => (pair.Value, Modifier(attributes, pair.Key)))
            .Where(pair => pair.Item2 == modifier)
            .Select(pair => (AttributeKind?)pair.Value)
            .FirstOrDefault();

        // The export does not name the tradition on the proficiency, so the label is the honest
        // generic one rather than a tradition picked out of the spell list.
        return new Spellcasting("Spell", rank, attribute ?? AttributeKind.Charisma);
    }

    static ImmutableArray<SheetEntry> ReadFeats(JsonElement content)
    {
        var groups = new (string Key, string Kind)[]
        {
            ("classFeats", "Class Feat"),
            ("ancestryFeats", "Ancestry Feat"),
            ("generalAndSkillFeats", "Skill or General Feat"),
            ("otherFeats", "Feat"),
            ("classFeatures", "Class Feature"),
            ("heritages", "Heritage"),
        };

        if (!content.TryGetProperty("feats_features", out var features)
            || features.ValueKind is not JsonValueKind.Object)
        {
            return [];
        }

        var taken = new List<SheetEntry>();
        foreach (var (key, kind) in groups)
        {
            if (!features.TryGetProperty(key, out var list) || list.ValueKind is not JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in list.EnumerateArray().Where(e => e.ValueKind is JsonValueKind.Object))
            {
                var name = String(entry, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // A heritage is written at level -1, which is the export saying "not at a
                    // level" rather than a level before first.
                    taken.Add(new SheetEntry(name!, kind, Math.Max(1, Int(entry, "level", 1)), null));
                }
            }
        }

        return [.. taken.DistinctBy(entry => (entry.Name, entry.Kind))];
    }

    static ImmutableArray<SheetEntry> ReadSpells(JsonElement content)
    {
        var spells = Object(content, "spells");
        if (spells is not { } all || !all.TryGetProperty("all", out var list)
            || list.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var known = new List<SheetEntry>();
        foreach (var spell in list.EnumerateArray().Where(s => s.ValueKind is JsonValueKind.Object))
        {
            var name = String(spell, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var rank = Int(spell, "rank", Int(spell, "level", 0));
            known.Add(new SheetEntry(name!, rank == 0 ? "Cantrip" : $"Rank {rank}", rank, null));
        }

        return [.. known.DistinctBy(entry => (entry.Name, entry.Level))];
    }

    /// <summary>A potency rune raises the armour's own item bonus, which is the same arithmetic
    /// the Pathbuilder path does with its "pot" field.</summary>
    static int Potency(JsonElement? armorMeta) =>
        Object(armorMeta, "runes") is { } runes ? Int(runes, "potency", 0) : 0;

    static int Modifier(JsonElement? attributes, string key) =>
        Object(attributes, key) is { } attribute ? Int(attribute, "value", 0) : 0;

    static ProficiencyRank Rank(JsonElement? proficiencies, string key) =>
        RankByBonus.GetValueOrDefault(Part(proficiencies, key, "profValue"), ProficiencyRank.Untrained);

    /// <summary>One number out of a proficiency's own breakdown: its rank bonus, or the
    /// attribute modifier that went into it.</summary>
    static int Part(JsonElement? proficiencies, string key, string part) =>
        Int(Object(Object(proficiencies, key), "parts"), part, 0);

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
}
