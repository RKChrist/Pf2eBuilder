using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// One combatant's initiative, set on its own.
/// <para>Rolling initiative starts the fight: it gives everyone a number, sets the round to one
/// and puts the marker on whoever goes first. None of that is what a GM wants when a player
/// says "sorry, I said twenty-one, it was twelve" on round three, and rolling again to fix one
/// number would re-roll every other creature in the fight. This changes the number and nothing
/// else. The order re-sorts around it; whose turn it is does not move, because a correction to
/// a number is not a turn passing.</para>
/// </summary>
public sealed record SetInitiative(string Code, string? DmKey, Guid CombatantId, int Initiative)
    : IRequest<CampaignView>;

public sealed class SetInitiativeValidator : AbstractValidator<SetInitiative>
{
    public SetInitiativeValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();

        // The same bounds rolling uses. A d20 and a modifier cannot leave this range, and a
        // number outside it is a typing slip rather than a fight.
        RuleFor(c => c.Initiative).InclusiveBetween(-20, 60)
                                  .WithMessage("An initiative is between -20 and 60.");
    }
}

public sealed class SetInitiativeHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<SetInitiative, CampaignView>
{
    public async Task<CampaignView> Handle(SetInitiative command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "an initiative", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "set an initiative");

        if (campaign.Encounter?.Find(command.CombatantId) is not { } combatant)
        {
            throw new CombatantNotFoundException("Nobody in this encounter has that id.");
        }

        combatant.Initiative = command.Initiative;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

/// <summary>
/// One combatant leaves the fight.
/// <para>A monster added by mistake, a monster that fled, a player who left the table. Without
/// this the only way out was to end the encounter, which ends it for everybody.</para>
/// <para>Effects that reached a monster leave with it, for the same reason they do when the
/// encounter ends: the creature they were on no longer exists to carry them. A player character
/// keeps theirs, because a character outlives the fight and so does being frightened.</para>
/// </summary>
public sealed record RemoveCombatant(string Code, string? DmKey, Guid CombatantId)
    : IRequest<CampaignView>;

public sealed class RemoveCombatantValidator : AbstractValidator<RemoveCombatant>
{
    public RemoveCombatantValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();
    }
}

public sealed class RemoveCombatantHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RemoveCombatant, CampaignView>
{
    public async Task<CampaignView> Handle(RemoveCombatant command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a combatant leaving", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "remove a combatant");

        if (campaign.Encounter is not { } encounter
            || encounter.Find(command.CombatantId) is not { } combatant)
        {
            throw new CombatantNotFoundException("Nobody in this encounter has that id.");
        }

        // Whose turn it is has to move before the row it points at is gone, or the encounter is
        // left pointing at nothing and the next turn has nowhere to start from.
        if (encounter.CurrentCombatantId == combatant.Id)
        {
            var order = encounter.Order();
            var at = order.IndexOf(order.First(entry => entry.Id == combatant.Id));
            encounter.CurrentCombatantId = order.Length > 1
                ? order[(at + 1) % order.Length].Id
                : null;
        }

        encounter.Combatants.Remove(combatant);
        encounter.Reminders = [.. encounter.Reminders.Where(r => r.CreatureId != combatant.Id)];

        if (combatant is MonsterCombatant)
        {
            foreach (var application in campaign.EffectApplications)
            {
                application.Targets.RemoveAll(target => target.TargetId == combatant.Id);
            }

            campaign.EffectApplications.RemoveAll(application => application.Targets.Count == 0);
        }

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}
