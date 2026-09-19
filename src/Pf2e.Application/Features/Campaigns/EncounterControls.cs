using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Reveal is one boolean and not three steps. Unrevealed means the monster is not in a player's
/// combatant list at all; revealed means a player sees its name, its initiative and its
/// conditions. Neither setting puts its hit points or its stat line in a player's payload.
/// </summary>
public sealed record RevealMonster(string Code, string? DmKey, Guid CombatantId, bool Revealed)
    : IRequest<CampaignView>;

public sealed class RevealMonsterValidator : AbstractValidator<RevealMonster>
{
    public RevealMonsterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();
    }
}

public sealed class RevealMonsterHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RevealMonster, CampaignView>
{
    public async Task<CampaignView> Handle(RevealMonster command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        CampaignAccess.RequireDm(role, "reveal a monster");

        if (campaign.Encounter?.Find(command.CombatantId) is not MonsterCombatant monster)
        {
            throw new CombatantNotFoundException("No monster in this encounter has that id.");
        }

        monster.Revealed = command.Revealed;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }
}

/// <summary>
/// The encounter ends and the campaign returns to Exploration, which is the other half of the
/// document's state diagram. The encounter and its combatants go with it; effects that outlast
/// the fight stay, because they belong to the campaign and not to the encounter.
/// </summary>
public sealed record EndEncounter(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class EndEncounterValidator : AbstractValidator<EndEncounter>
{
    public EndEncounterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
    }
}

public sealed class EndEncounterHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<EndEncounter, CampaignView>
{
    public async Task<CampaignView> Handle(EndEncounter command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        CampaignAccess.RequireDm(role, "end the encounter");

        if (campaign.Encounter is { } encounter)
        {
            // Effects that reached a monster leave with the monster, because the creature they
            // were on no longer exists to carry them.
            var gone = encounter.Combatants.OfType<MonsterCombatant>().Select(m => m.Id).ToHashSet();
            foreach (var application in campaign.EffectApplications)
            {
                application.Targets.RemoveAll(target => gone.Contains(target.TargetId));
            }

            campaign.EffectApplications.RemoveAll(application => application.Targets.Count == 0);
            campaign.Encounter = null;
        }

        campaign.Mode = CampaignMode.Exploration;

        await db.SaveChangesAsync(ct);
        await broadcaster.ModeChangedAsync(
            campaign.Code, new CampaignModeView(campaign.Code, campaign.Mode.ToString()), ct);

        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }
}
