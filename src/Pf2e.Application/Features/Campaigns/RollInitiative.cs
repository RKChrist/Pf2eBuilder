using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Rolling initiative is what enters Encounter mode, which is what the document's state diagram
/// says. A combatant the request names takes the number the request gives, because at a real
/// table the players roll their own dice; anything it does not name is rolled here.
/// </summary>
public sealed record RollInitiative(string Code, string? DmKey, IReadOnlyList<InitiativeRoll> Rolls)
    : IRequest<CampaignView>;

public sealed class RollInitiativeValidator : AbstractValidator<RollInitiative>
{
    public RollInitiativeValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleForEach(c => c.Rolls).ChildRules(roll =>
        {
            roll.RuleFor(r => r.CombatantId).NotEmpty();
            roll.RuleFor(r => r.Initiative).InclusiveBetween(-20, 60);
        });
    }
}

public sealed class RollInitiativeHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RollInitiative, CampaignView>
{
    public async Task<CampaignView> Handle(RollInitiative command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "initiative", ct);
        CampaignAccess.RequireDm(role, "roll initiative");

        if (campaign.Encounter is not { Combatants.Count: > 0 } encounter)
        {
            throw new CombatantNotFoundException(
                "Nobody is in this encounter yet. Add the party and the monsters first.");
        }

        var given = command.Rolls.ToDictionary(roll => roll.CombatantId, roll => roll.Initiative);

        foreach (var combatant in encounter.Combatants)
        {
            combatant.Initiative = given.TryGetValue(combatant.Id, out var stated)
                ? stated
                : Rolled(campaign, combatant);
        }

        encounter.Round = 1;
        encounter.Reminders = [];

        // The marker is set from the sorted order, so the tie rule decides who goes first here
        // in exactly the same way it decides every later turn.
        encounter.CurrentCombatantId = encounter.Order()[0].Id;

        campaign.Mode = CampaignMode.Encounter;

        await db.SaveChangesAsync(ct);
        await broadcaster.ModeChangedAsync(
            campaign.Code, new CampaignModeView(campaign.Code, campaign.Mode.ToString()), ct);

        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }

    /// <summary>A d20 plus the creature's Perception, which is what initiative usually is.</summary>
    internal static int Rolled(Campaign campaign, Combatant combatant)
    {
        var perception = combatant switch
        {
            MonsterCombatant monster => monster.Stats.Perception,
            _ => campaign.Characters.FirstOrDefault(c => c.Id == combatant.Id) is { } character
                ? CharacterSheet.Compute(character.ToBuild(), character.ToSession(campaign.EffectApplications))
                                .Perception.Total
                : 0,
        };

        return RandomNumberGenerator.GetInt32(1, 21) + perception;
    }
}
