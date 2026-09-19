using System.Text.Json.Nodes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

public sealed record ImportCharacter(string Code, string Pathbuilder) : IRequest<CharacterSheetView>;

public sealed class ImportCharacterValidator : AbstractValidator<ImportCharacter>
{
    // A Pathbuilder export is a few kilobytes. A Wanderer's Guide one is thirteen megabytes for
    // a level 13 rogue, because it embeds the full record of every item and spell it touched;
    // the parts the importer reads come to about 150 KB and the rest is walked past. The cap is
    // here to stop somebody pasting a film, so it is set above the format that legitimately
    // needs the room rather than below it.
    public const int MaxPayload = 24 * 1024 * 1024;

    public ImportCharacterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                                 .WithMessage("A campaign code is four to twelve letters and digits.");
        // Cascade.Stop, because without it FluentValidation runs every rule in the chain and
        // the length check dereferences the null that NotEmpty just rejected. A request with
        // the field missing then answers 500 instead of naming the missing field.
        RuleFor(c => c.Pathbuilder).Cascade(CascadeMode.Stop)
                                   .NotEmpty()
                                   .Must(payload => payload.Length <= MaxPayload)
                                   .WithMessage($"A character export is smaller than {MaxPayload / (1024 * 1024)} MB.");
    }
}

public sealed class ImportCharacterHandler(
    ITrackerDbContext tracker,
    IRulesDbContext rules,
    ICampaignBroadcaster broadcaster) : IRequestHandler<ImportCharacter, CharacterSheetView>
{
    public async Task<CharacterSheetView> Handle(ImportCharacter command, CancellationToken ct)
    {
        // A campaign nobody created is a refusal that names the problem, not a campaign this
        // handler starts on the way past. One started here would have a DM key that reached
        // nobody, which is a campaign with no DM.
        //
        // Checked before the paste is parsed, because if there is nowhere to put the character
        // then what the player pasted is beside the point, and being told about their JSON when
        // the real problem is the code sends them to fix the wrong thing.
        var (campaign, _) = await CampaignAccess.LoadAsync(tracker, command.Code, null, ct);
        var code = campaign.Code;

        var parsed = PathbuilderBuild.Parse(command.Pathbuilder);
        var build = await WithSeededGear(parsed, ct);

        // Pathbuilder stores one JSON id per player and overwrites it on each export, so it is a
        // slot and not an identity. Matching on the name is what makes a level-up a re-import.
        var character = campaign.Characters
            .FirstOrDefault(c => string.Equals(c.Name, build.Name, StringComparison.OrdinalIgnoreCase));

        if (character is null)
        {
            var fresh = SessionState.Fresh(CharacterSheet.Compute(build, SessionState.Fresh(0)).MaxHitPoints);
            character = TrackedCharacter.From(campaign.Id, build, fresh);
            campaign.Characters.Add(character);
        }
        else
        {
            character.Apply(build);
        }

        await tracker.SaveChangesAsync(ct);

        var sheet = SheetViews.Of(character, campaign.EffectApplications);
        await broadcaster.CharacterChangedAsync(code, sheet, ct);
        return sheet;
    }

    /// <summary>
    /// The armour's item bonus and Dex cap come from the seeded ruleset rather than a campaign in
    /// this file, so a re-seed corrects them. A potency rune raises the armour's own item bonus,
    /// so +1 studded leather is one +3 item bonus and not a +2 and a +1 that the stacking rule
    /// would then refuse to combine.
    /// </summary>
    async Task<Character> WithSeededGear(PathbuilderBuild parsed, CancellationToken ct) =>
        await WithSeededEntries(await WithSeededWeapons(await WithSeededArmor(parsed, ct), ct), ct);

    /// <summary>
    /// Joins the feats and spells the export named to the records the ruleset holds, so the
    /// reference screen can open the rule rather than only printing the name.
    /// <para>Matched on the name, in memory, over the two categories rather than by a provider
    /// whose collation decides whether "fear" finds "Fear". A name the ruleset does not hold
    /// keeps a null id and still shows: homebrew is somebody's character too, and a feat newer
    /// than the snapshot is not the player's mistake.</para>
    /// </summary>
    async Task<Character> WithSeededEntries(Character build, CancellationToken ct)
    {
        if (build.Feats.Length == 0 && build.Spells.Length == 0)
        {
            return build;
        }

        var wanted = build.Feats.Concat(build.Spells)
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var records = (await rules.RuleRecords.AsNoTracking()
                .Where(r => r.Category == "feat" || r.Category == "spell")
                .Select(r => new { r.Id, r.Name, r.Category })
                .ToListAsync(ct))
            .Where(r => wanted.Contains(r.Name))
            .ToList();

        // An export written before the Remaster carries the old names, so a name that matches
        // nothing is tried again against the rename index. Inspire Competence is Uplifting
        // Overture now, and a character sheet that showed the old name and opened nothing was
        // telling the player their own feat did not exist.
        var renamed = (await rules.RuleAliases.AsNoTracking()
                .Where(a => a.Category == "feat" || a.Category == "spell")
                .ToListAsync(ct))
            .Where(a => wanted.Contains(a.Was))
            .ToDictionary(a => (a.Category, a.Was), a => a.NowId, TupleComparer);

        string? Resolve(string category, string name) =>
            records.FirstOrDefault(r =>
                r.Category == category
                && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))?.Id
            ?? (renamed.TryGetValue((category, name), out var nowId) ? nowId : null);

        return build with
        {
            Feats = [.. build.Feats.Select(feat => feat with { RuleId = Resolve("feat", feat.Name) })],
            Spells = [.. build.Spells.Select(spell => spell with { RuleId = Resolve("spell", spell.Name) })],
        };
    }

    /// <summary>
    /// Which attribute governs an attack is not in the export and is in the ruleset: a ranged
    /// weapon uses Dexterity, a finesse weapon the better of Strength and Dexterity, and anything
    /// else Strength. It matters because a selector reads it. Clumsy has to reach a rapier and
    /// must not reach a greatsword, and leaving every weapon on Strength gets both wrong.
    /// </summary>
    async Task<Character> WithSeededWeapons(Character build, CancellationToken ct)
    {
        if (build.Weapons.Length == 0)
        {
            return build;
        }

        var names = build.Weapons.Select(w => w.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Traits are a column of their own and not part of the mechanics JSON, because the seeder
        // promotes them out of it. Reading mechanics["trait"] here found nothing and quietly made
        // every weapon Strength-governed, which a clumsy rapier is not.
        var seeded = (await rules.RuleRecords.AsNoTracking()
                .Where(r => r.Category == "weapon")
                .Select(r => new { r.Name, r.Traits, r.Mechanics })
                .ToListAsync(ct))
            .Where(r => names.Contains(r.Name))
            .ToDictionary(
                r => r.Name,
                r => (r.Traits, Mechanics: JsonNode.Parse(r.Mechanics) as JsonObject),
                StringComparer.OrdinalIgnoreCase);

        return build with
        {
            Weapons =
            [
                .. build.Weapons.Select(weapon =>
                {
                    var record = seeded.TryGetValue(weapon.Name, out var found) ? found : default;
                    return weapon with { GovernedBy = Governs(record.Traits, record.Mechanics, build.Attributes) };
                }),
            ],
        };
    }

    static AttributeKind Governs(List<string>? traits, JsonObject? mechanics, AttributeModifiers attributes)
    {
        // A weapon this ruleset does not hold keeps Strength, which is the commonest answer and
        // the one a reader can spot as wrong.
        if (mechanics is null)
        {
            return AttributeKind.Strength;
        }

        if (string.Equals(mechanics["weapon_type"]?.GetValue<string>(), "Ranged", StringComparison.OrdinalIgnoreCase))
        {
            return AttributeKind.Dexterity;
        }

        var finesse = traits?.Contains("Finesse", StringComparer.OrdinalIgnoreCase) ?? false;

        return finesse && attributes.Dexterity > attributes.Strength
            ? AttributeKind.Dexterity
            : AttributeKind.Strength;
    }

    async Task<Character> WithSeededArmor(PathbuilderBuild parsed, CancellationToken ct)
    {
        // All 42 armour records are matched in memory rather than by a provider whose collation
        // decides whether "studded leather" finds "Studded Leather Armor".
        var armor = await rules.RuleRecords.AsNoTracking()
            .Where(r => r.Category == "armor")
            .Select(r => new { r.Name, r.Mechanics })
            .ToListAsync(ct);

        var name = parsed.Build.ArmorName;
        var record =
            armor.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
            // Pathbuilder exports "Studded Leather" where the Remaster record is "Studded Leather Armor".
            ?? armor.FirstOrDefault(a => string.Equals(a.Name, $"{name} Armor", StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            return parsed.Build with { ArmorName = "Unarmored" };
        }

        var mechanics = JsonNode.Parse(record.Mechanics) as JsonObject;
        return parsed.Build with
        {
            ArmorName = record.Name,
            ArmorItemBonus = Number(mechanics, "ac", 0) + parsed.ArmorPotency,
            ArmorDexCap = mechanics?["dex_cap"] is JsonValue cap && cap.TryGetValue(out int value) ? value : null,
        };
    }

    /// <summary>Case-insensitive on the name, because an export's capitalisation is its own.</summary>
    static readonly IEqualityComparer<(string Category, string Was)> TupleComparer =
        EqualityComparer<(string Category, string Was)>.Create(
            (a, b) => string.Equals(a.Category, b.Category, StringComparison.Ordinal)
                      && string.Equals(a.Was, b.Was, StringComparison.OrdinalIgnoreCase),
            value => HashCode.Combine(value.Category, value.Was.ToLowerInvariant()));

    static int Number(JsonObject? mechanics, string key, int fallback) =>
        mechanics?[key] is JsonValue value && value.TryGetValue(out int number) ? number : fallback;
}
