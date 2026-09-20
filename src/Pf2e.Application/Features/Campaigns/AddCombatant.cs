using System.Text.Json.Nodes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Putting a creature into the fight is the encounter's own job, not something you leave the
/// screen to do elsewhere. A monster comes from the seeded bestiary by its record id, with real
/// numbers; a player comes from the campaign's roster.
/// <para>The encounter is created here if there is not one yet, so the first combatant starts
/// the fight. Mode does not move until initiative is rolled, which is what the document's state
/// diagram says enters Encounter.</para>
/// </summary>
public sealed record AddCombatant(
    string Code, string? DmKey, string? RuleId, Guid? CharacterId, string? Name, int? Initiative = null)
    : IRequest<CampaignView>;

public sealed class AddCombatantValidator : AbstractValidator<AddCombatant>
{
    public AddCombatantValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");

        // Exactly one, because a request naming both has not said what it wants added.
        RuleFor(c => c).Must(c => c.RuleId is { Length: > 0 } ^ c.CharacterId is not null)
                       .WithMessage("Name either a creature record to add as a monster or a character to add.");

        RuleFor(c => c.Name).MaximumLength(128).When(c => c.Name is not null);
        RuleFor(c => c.Initiative).InclusiveBetween(-20, 60).When(c => c.Initiative is not null);
    }
}

public sealed class AddCombatantHandler(
    ITrackerDbContext db, IRulesDbContext rules, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<AddCombatant, CampaignView>
{
    public async Task<CampaignView> Handle(AddCombatant command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a combatant", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "add a combatant");

        var encounter = campaign.Encounter ??= new Encounter
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
        };

        var added = command.CharacterId is { } characterId
            ? AddPlayer(campaign, encounter, characterId)
            : Added(encounter, await MonsterAsync(encounter, command, ct));

        // A reinforcement arriving mid-fight takes its initiative now and the marker is left
        // exactly where it was, because whose turn it is has not changed. Rolling initiative
        // again would restart the fight, which is a different thing the DM has a button for.
        if (added is not null && encounter.Round > 0)
        {
            added.Initiative = command.Initiative ?? RollInitiativeHandler.Rolled(campaign, added);
        }

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }

    static Combatant Added(Encounter encounter, Combatant combatant)
    {
        encounter.Combatants.Add(combatant);
        return combatant;
    }

    /// <summary>
    /// A player combatant's id is the character's id, so adding the same character twice is the
    /// same row rather than a second entry in the initiative order.
    /// </summary>
    static Combatant? AddPlayer(Campaign campaign, Encounter encounter, Guid characterId)
    {
        if (campaign.Characters.All(c => c.Id != characterId))
        {
            throw new CombatantNotFoundException(
                "That character is not in this campaign's roster. Import them first.");
        }

        // Null for a character already in the fight, so a second add is not a second entry in
        // the initiative order and does not reroll the one that is there.
        return encounter.Find(characterId) is not null
            ? null
            : Added(encounter, new PlayerCombatant { Id = characterId, EncounterId = encounter.Id });
    }

    async Task<MonsterCombatant> MonsterAsync(Encounter encounter, AddCombatant command, CancellationToken ct)
    {
        var record = await rules.RuleRecords.AsNoTracking()
            .Where(r => r.Id == command.RuleId && r.Category == "creature")
            .Select(r => new { r.Id, r.Name, r.Level, r.Traits, r.Mechanics })
            .SingleOrDefaultAsync(ct)
            ?? throw new CombatantNotFoundException(
                $"No seeded creature has the id {command.RuleId}.");

        var mechanics = JsonNode.Parse(record.Mechanics) as JsonObject;
        var stats = new MonsterStatBlock(
            record.Level ?? 0,
            Number(mechanics, "hp"),
            Number(mechanics, "ac"),
            Number(mechanics, "fortitude_save"),
            Number(mechanics, "reflex_save"),
            Number(mechanics, "will_save"),
            Number(mechanics, "perception"),
            [.. record.Traits]);

        return new MonsterCombatant
        {
            // A fresh id per instance, because two ogres in one fight are two creatures that
            // take damage separately.
            Id = Guid.NewGuid(),
            EncounterId = encounter.Id,
            Name = command.Name is { Length: > 0 } named ? named : Numbered(encounter, record.Name),
            RuleId = record.Id,
            Stats = stats,
            CurrentHitPoints = stats.MaxHitPoints,

            // Unrevealed, so the first thing that happens to a monster is that the players
            // cannot see it. Revealing is the DM's deliberate act.
            Revealed = false,
        };
    }

    /// <summary>
    /// What to call this one when the fight already holds another of the same creature. The
    /// second arrival numbers itself and renames the first, so a lone ogre is "Ogre Warrior" and
    /// the moment there are two they are "Ogre Warrior 1" and "Ogre Warrior 2".
    /// <para>Counted over the rows that are actually in the order, so what this guarantees is
    /// that no two of them ever read the same. A number is not retired: once a creature is dead
    /// and gone a new arrival may take it back, which is unambiguous because there is nothing
    /// left on the screen to confuse it with.</para>
    /// </summary>
    static string Numbered(Encounter encounter, string name)
    {
        var sameCreature = encounter.Combatants
            .OfType<MonsterCombatant>()
            .Where(m => m.Name == name || m.Name.StartsWith(name + " ", StringComparison.Ordinal))
            .ToList();

        if (sameCreature.Count == 0)
        {
            return name;
        }

        if (sameCreature.SingleOrDefault(m => m.Name == name) is { } lonely)
        {
            lonely.Name = $"{name} 1";
        }

        var highest = sameCreature
            .Select(m => int.TryParse(m.Name[name.Length..].Trim(), out var n) ? n : 1)
            .DefaultIfEmpty(0)
            .Max();

        return $"{name} {highest + 1}";
    }

    static int Number(JsonObject? mechanics, string key) =>
        mechanics?[key] is JsonValue value && value.TryGetValue(out int number) ? number : 0;
}

/// <summary>Named rather than a bare 404, because "which creature" is the whole question.</summary>
public sealed class CombatantNotFoundException(string message) : Exception(message);
