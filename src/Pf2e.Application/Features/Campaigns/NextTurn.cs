using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Advances the marker, counts durations down by the ruleset's timing, expires what has run
/// out, decrements frightened at the end of the affected creature's turn, and leaves a reminder
/// about persistent damage.
/// <para>All of that timing is <see cref="TurnClock"/>, in the domain, where it can be asserted
/// without a database. This handler decides who is next and saves the result.</para>
/// </summary>
public sealed record NextTurn(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class NextTurnValidator : AbstractValidator<NextTurn>
{
    public NextTurnValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
    }
}

public sealed class NextTurnHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<NextTurn, CampaignView>
{
    public async Task<CampaignView> Handle(NextTurn command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "the turn", ct);
        CampaignAccess.RequireDm(role, "advance the turn");

        if (campaign.Encounter is not { Round: > 0 } encounter)
        {
            throw new CombatantNotFoundException(
                "This encounter has not started. Roll initiative first.");
        }

        // The order is rebuilt from the combatants as they now stand, and the marker is looked
        // up in it by id. A combatant added since the last turn therefore lands in its place
        // without moving whose turn it is.
        if (TurnOrder.Advance(encounter.Order(), encounter.CurrentCombatantId) is not { } advance)
        {
            throw new CombatantNotFoundException("Nobody is left in this encounter.");
        }

        if (advance.NewRound)
        {
            encounter.Round += 1;
        }

        encounter.CurrentCombatantId = advance.Starting;

        var tick = new TurnTick(advance.Ending, advance.Starting, encounter.Round);
        var reminders = TurnClock.Run(
            tick, campaign.EffectApplications, id => encounter.NameOf(id, campaign.Characters));

        encounter.Reminders = [.. reminders];

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }
}
