using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>Nothing to put back, which is a sentence and not a 500.</summary>
public sealed class NothingToUndoException() : Exception(
    "There is nothing to undo. The stack is held in memory for this session only, so a restart " +
    "empties it and so does ending an encounter.");

/// <summary>
/// Puts the campaign back to the state before the last change. A DM control, because it can
/// reverse anything anybody did and a player reversing the DM's damage is not an undo.
/// </summary>
public sealed record UndoLastChange(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class UndoLastChangeValidator : AbstractValidator<UndoLastChange>
{
    public UndoLastChangeValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
    }
}

public sealed class UndoLastChangeHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<UndoLastChange, CampaignView>
{
    public async Task<CampaignView> Handle(UndoLastChange command, CancellationToken ct)
    {
        // Loaded without a snapshot, because undoing an undo is a redo and that is a different
        // feature. Popping and restoring leaves the stack one shorter, which is what a DM
        // tapping the button twice expects.
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        CampaignAccess.RequireDm(role, "undo");

        if (undo.Pop(campaign.Id) is not { } snapshot)
        {
            throw new NothingToUndoException();
        }

        CampaignSnapshots.Restore(campaign, snapshot);
        await db.SaveChangesAsync(ct);

        await broadcaster.ModeChangedAsync(
            campaign.Code, new CampaignModeView(campaign.Code, campaign.Mode.ToString()), ct);

        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }
}
