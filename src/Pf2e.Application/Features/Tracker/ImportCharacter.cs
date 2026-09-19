using System.Text.Json.Nodes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Tracker;

public sealed record ImportCharacter(string TableCode, string Pathbuilder) : IRequest<CharacterSheetView>;

public sealed class ImportCharacterValidator : AbstractValidator<ImportCharacter>
{
    const int MaxPayload = 512 * 1024;

    public ImportCharacterValidator()
    {
        RuleFor(c => c.TableCode).Must(TableCode.IsValid)
                                 .WithMessage("A table code is four to twelve letters and digits.");
        // Cascade.Stop, because without it FluentValidation runs every rule in the chain and
        // the length check dereferences the null that NotEmpty just rejected. A request with
        // the field missing then answers 500 instead of naming the missing field.
        RuleFor(c => c.Pathbuilder).Cascade(CascadeMode.Stop)
                                   .NotEmpty()
                                   .Must(payload => payload.Length <= MaxPayload)
                                   .WithMessage("A Pathbuilder export is smaller than 512 KB.");
    }
}

public sealed class ImportCharacterHandler(
    ITrackerDbContext tracker,
    IRulesDbContext rules,
    ITableBroadcaster broadcaster) : IRequestHandler<ImportCharacter, CharacterSheetView>
{
    public async Task<CharacterSheetView> Handle(ImportCharacter command, CancellationToken ct)
    {
        var parsed = PathbuilderBuild.Parse(command.Pathbuilder);
        var build = await WithSeededArmor(parsed, ct);
        var code = TableCode.Normalize(command.TableCode);

        var table = await tracker.Tables
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        if (table is null)
        {
            // A table comes into being when its first character arrives, which is why there is
            // no operation that creates one.
            table = new TrackedTable { Id = Guid.NewGuid(), Code = code, CreatedAtUtc = DateTimeOffset.UtcNow };
            tracker.Tables.Add(table);
        }

        // Pathbuilder stores one JSON id per player and overwrites it on each export, so it is a
        // slot and not an identity. Matching on the name is what makes a level-up a re-import.
        var character = table.Characters
            .FirstOrDefault(c => string.Equals(c.Name, build.Name, StringComparison.OrdinalIgnoreCase));

        if (character is null)
        {
            var fresh = SessionState.Fresh(CharacterSheet.Compute(build, SessionState.Fresh(0)).MaxHitPoints);
            character = TrackedCharacter.From(table.Id, build, fresh);
            table.Characters.Add(character);
        }
        else
        {
            character.Apply(build);
        }

        await tracker.SaveChangesAsync(ct);

        var sheet = SheetViews.Of(character);
        await broadcaster.CharacterChangedAsync(code, sheet, ct);
        return sheet;
    }

    /// <summary>
    /// The armour's item bonus and Dex cap come from the seeded ruleset rather than a table in
    /// this file, so a re-seed corrects them. A potency rune raises the armour's own item bonus,
    /// so +1 studded leather is one +3 item bonus and not a +2 and a +1 that the stacking rule
    /// would then refuse to combine.
    /// </summary>
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

    static int Number(JsonObject? mechanics, string key, int fallback) =>
        mechanics?[key] is JsonValue value && value.TryGetValue(out int number) ? number : fallback;
}
