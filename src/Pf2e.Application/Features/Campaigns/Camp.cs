using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>A ten-minute activity, by one character. Open to anyone at the table, like an
/// exploration activity: the medic says they are Treating Wounds, not the DM.</summary>
public sealed record TakeCampActivity(string Code, string? DmKey, Guid CharacterId, string Activity)
    : IRequest<CampaignView>;

/// <summary>Eight hours. The DM's, because it moves the world on rather than one character.</summary>
public sealed record RestForTheNight(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class TakeCampActivityValidator : AbstractValidator<TakeCampActivity>
{
    public TakeCampActivityValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Activity).Must(key => CampActivities.Find(key) is not null)
                                .WithMessage("That is not a camp activity.");
    }
}

public sealed class RestForTheNightValidator : AbstractValidator<RestForTheNight>
{
    public RestForTheNightValidator() =>
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
}

/// <summary>A refusal the player can act on, which is the only kind worth sending.</summary>
public sealed class StillImmuneException(string message) : Exception(message);

public sealed class TakeCampActivityHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<TakeCampActivity, CampaignView>
{
    public async Task<CampaignView> Handle(TakeCampActivity command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "camp activity", ct);
        var campaign = change.Campaign;

        var character = campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId)
            ?? throw new CombatantNotFoundException("That character is not in this campaign.");

        var activity = CampActivities.Find(command.Activity)!;

        var treating = activity.Key == CampActivities.TreatWounds.Key;

        if (treating && character.TreatedAtMinute is { } treated)
        {
            // The immunity is the whole reason this is a command rather than a note on a page.
            var since = campaign.ElapsedMinutes - treated;
            if (since < CampActivities.TreatWoundsImmunityMinutes)
            {
                var left = CampActivities.TreatWoundsImmunityMinutes - since;
                throw new StillImmuneException(
                    $"{character.Name} was Treated {since} minutes ago and is immune for another {left}.");
            }
        }

        // The clock moves whatever the activity was, because ten minutes spent is ten minutes
        // spent even when the check failed.
        campaign.ElapsedMinutes += activity.Minutes;

        // Marked after the clock moves, not before. The printed rule says the target is immune
        // for an hour once the activity is done, and marking first started the hour ten minutes
        // early: a character read as immune for fifty minutes the instant they were Treated.
        if (treating)
        {
            character.TreatedAtMinute = campaign.ElapsedMinutes;
        }

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

public sealed class RestForTheNightHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RestForTheNight, CampaignView>
{
    public async Task<CampaignView> Handle(RestForTheNight command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a night's rest", ct);
        var campaign = change.Campaign;
        CampaignAccess.RequireDm(change.Role, "call a night's rest");

        campaign.ElapsedMinutes += NightsRest.Minutes;

        foreach (var character in campaign.Characters)
        {
            var build = character.ToBuild();
            var sheet = CharacterSheet.Compute(build, character.ToSession(campaign.EffectApplications));

            character.CurrentHitPoints = Math.Min(
                sheet.MaxHitPoints,
                character.CurrentHitPoints + NightsRest.Recovery(build.Level, build.Attributes.Constitution));

            // Temporary hit points are temporary. An hour of them surviving a night's sleep is a
            // bug people notice a session later and cannot explain.
            character.TemporaryHitPoints = 0;

            // An hour has certainly passed, so nobody is still immune in the morning.
            character.TreatedAtMinute = null;
        }

        RestConditions(campaign);

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }

    /// <summary>
    /// Fatigued ends, drained and doomed step down by one, and everything else is still there in
    /// the morning. A monster's conditions are left alone: whatever the party is sleeping next to
    /// did not get a night's rest by their doing.
    /// </summary>
    static void RestConditions(Campaign campaign)
    {
        var characters = campaign.Characters.Select(c => c.Id).ToHashSet();

        foreach (var application in campaign.EffectApplications.ToList())
        {
            foreach (var target in application.Targets.ToList())
            {
                if (target.Kind is not EffectTargetKind.Character || !characters.Contains(target.TargetId))
                {
                    continue;
                }

                var after = NightsRest.After(application.SourceKey, target.Value);
                if (after is null)
                {
                    application.Targets.Remove(target);
                    continue;
                }

                target.Value = after.Value;
            }

            // An application nobody is carrying any more is gone, which is the same rule the turn
            // clock uses when the last countdown runs out.
            if (application.Targets.Count == 0)
            {
                campaign.EffectApplications.Remove(application);
            }
        }
    }
}
