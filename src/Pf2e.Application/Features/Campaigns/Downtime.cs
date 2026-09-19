using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// What one character is spending the current downtime day on. Null clears it.
/// <para>Open to anyone at the table, like an exploration activity. The key rides along so the
/// projection answers the caller's own role rather than demoting a DM who used the screen.</para>
/// </summary>
public sealed record SetDowntimeActivity(
    string Code, string? DmKey, Guid CharacterId, string? Activity, int? TaskLevel)
    : IRequest<CampaignView>;

/// <summary>The day turns, and everybody starts a fresh one. The DM's, because it moves the
/// world on rather than one character.</summary>
public sealed record AdvanceDay(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class SetDowntimeActivityValidator : AbstractValidator<SetDowntimeActivity>
{
    public SetDowntimeActivityValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Activity).Must(key => DowntimeActivities.Find(key) is not null)
                                .When(c => c.Activity is not null)
                                .WithMessage("That is not a downtime activity.");
        RuleFor(c => c.TaskLevel).InclusiveBetween(LevelBasedDc.Lowest, LevelBasedDc.Highest)
                                 .When(c => c.TaskLevel is not null)
                                 .WithMessage($"A task level is {LevelBasedDc.Lowest} to {LevelBasedDc.Highest}.");
    }
}

public sealed class AdvanceDayValidator : AbstractValidator<AdvanceDay>
{
    public AdvanceDayValidator() =>
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
}

public sealed class SetDowntimeActivityHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<SetDowntimeActivity, CampaignView>
{
    public async Task<CampaignView> Handle(SetDowntimeActivity command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "downtime activity", ct);

        var character = change.Campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId)
            ?? throw new CombatantNotFoundException("That character is not in this campaign.");

        var activity = DowntimeActivities.Find(command.Activity);
        character.DowntimeActivity = activity?.Key;

        // A task level with no activity is a level for nothing, and it would sit there looking
        // like a DC once somebody picked something else.
        character.DowntimeTaskLevel = activity is null ? null : command.TaskLevel;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

public sealed class AdvanceDayHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<AdvanceDay, CampaignView>
{
    public async Task<CampaignView> Handle(AdvanceDay command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "the day turning", ct);
        var campaign = change.Campaign;
        CampaignAccess.RequireDm(change.Role, "turn the day");

        campaign.Day += 1;

        // One activity per character per day is the rule, so the choices go with the day. Leaving
        // them would make the next morning look like it had already been decided.
        foreach (var character in campaign.Characters)
        {
            character.DowntimeActivity = null;
            character.DowntimeTaskLevel = null;
        }

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}
