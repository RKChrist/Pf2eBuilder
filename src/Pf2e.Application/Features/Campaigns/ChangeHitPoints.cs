using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// An amount and a direction, so losing 37 hit points is one request whatever the number. The
/// stepper sent one point per tap, which made the control unusable in the moment it exists for.
/// <para>It is still a delta and never an absolute. Two people applying damage at the same
/// moment must sum, and last-write-wins on an absolute silently loses one of them; the
/// direction is what the caller types, and the signed number is what the database adds.</para>
/// </summary>
public sealed record ChangeHitPoints(
    string Code, string? DmKey, Guid CreatureId, int Amount, HitPointDirection Direction)
    : IRequest<CampaignView>;

public enum HitPointDirection
{
    Damage,
    Heal,
}

public sealed class ChangeHitPointsValidator : AbstractValidator<ChangeHitPoints>
{
    public ChangeHitPointsValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CreatureId).NotEmpty();
        RuleFor(c => c.Amount).GreaterThan(0).WithMessage("A change of no hit points is not a change.");
        RuleFor(c => c.Amount).LessThanOrEqualTo(999);
        RuleFor(c => c.Direction).IsInEnum().WithMessage("Direction is Damage or Heal.");
    }
}

public sealed class ChangeHitPointsHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<ChangeHitPoints, CampaignView>
{
    public async Task<CampaignView> Handle(ChangeHitPoints command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        var delta = command.Direction is HitPointDirection.Damage ? -command.Amount : command.Amount;

        if (campaign.Encounter?.Find(command.CreatureId) is MonsterCombatant monster)
        {
            // A monster's hit points are the DM's, both to read and to change.
            CampaignAccess.RequireDm(role, "change a monster's hit points");
            monster.CurrentHitPoints =
                HitPoints.AfterDelta(monster.CurrentHitPoints, delta, monster.Stats.MaxHitPoints);

            await db.SaveChangesAsync(ct);
            return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
        }

        if (campaign.Characters.FirstOrDefault(c => c.Id == command.CreatureId) is not { } character)
        {
            throw new CombatantNotFoundException("Nothing in this campaign has that id.");
        }

        var max = CharacterSheet
            .Compute(character.ToBuild(), character.ToSession(campaign.EffectApplications))
            .MaxHitPoints;

        // The arithmetic happens in the database, in one statement, so two people applying
        // damage at the same moment sum. Reading the value here and writing it back would
        // reinstate last-write-wins one layer below an API whose whole shape exists to prevent
        // it. The maximum is safe to carry from the read above: it moves only when the build or
        // drained changes, and this command changes neither.
        await db.Characters
            .Where(c => c.Id == character.Id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(
                    c => c.CurrentHitPoints,
                    c => Math.Max(0, Math.Min(max, c.CurrentHitPoints + delta))),
                ct);

        // Read back rather than adjusting the instance above, which the statement left stale.
        // The re-read is untracked, so identity resolution cannot hand back the same stale
        // object, which is what makes this a real re-read rather than one that looks like one.
        var (after, _) = await CampaignAccess.LoadAsync(db, campaign.Code, command.DmKey, ct, tracking: false);
        return await CampaignAccess.PublishAsync(broadcaster, after, role, ct);
    }
}
