using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>A signed delta and never an absolute. Two people applying damage at once must sum,
/// and last-write-wins on an absolute silently loses one of them.</summary>
public sealed record ChangeHitPoints(string Code, Guid CharacterId, int Delta)
    : IRequest<CharacterSheetView?>;

public sealed class ChangeHitPointsValidator : AbstractValidator<ChangeHitPoints>
{
    public ChangeHitPointsValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                                 .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.Delta).NotEqual(0).WithMessage("A change of no hit points is not a change.");
        RuleFor(c => c.Delta).InclusiveBetween(-999, 999);
    }
}

public sealed class ChangeHitPointsHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<ChangeHitPoints, CharacterSheetView?>
{
    public async Task<CharacterSheetView?> Handle(ChangeHitPoints command, CancellationToken ct)
    {
        var (campaign, _) = await CampaignAccess.LoadAsync(db, command.Code, null, ct, tracking: false);
        var code = campaign.Code;

        if (campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId) is not { } character)
        {
            return null;
        }

        var max = CharacterSheet
            .Compute(character.ToBuild(), character.ToSession(campaign.EffectApplications))
            .MaxHitPoints;
        var delta = command.Delta;

        // The arithmetic happens in the database, in one statement, so two people applying damage
        // at the same moment sum. Reading the value here and writing it back would reinstate
        // last-write-wins one layer below an API whose whole shape exists to prevent it. The
        // maximum is safe to carry from the read above: it moves only when the build or drained
        // changes, and this command changes neither.
        await db.Characters
            .Where(c => c.Id == character.Id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(
                    c => c.CurrentHitPoints,
                    c => Math.Max(0, Math.Min(max, c.CurrentHitPoints + delta))),
                ct);

        // Read back rather than adjusting the instance above, which the statement left stale.
        // Both reads are untracked, so this cannot be handed the same stale object by identity
        // resolution, which is what makes it a real re-read rather than one that looks like one.
        var (after, _) = await CampaignAccess.LoadAsync(db, code, null, ct, tracking: false);

        if (after.Characters.FirstOrDefault(c => c.Id == command.CharacterId) is not { } updated)
        {
            return null;
        }

        var sheet = SheetViews.Of(updated, after.EffectApplications);
        await broadcaster.CharacterChangedAsync(code, sheet, ct);
        return sheet;
    }
}
