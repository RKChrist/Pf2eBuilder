using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Switching mode is a DM action and every screen follows, so the change is written to the
/// campaign and pushed to every connection rather than kept in whoever tapped it.
/// </summary>
public sealed record SetMode(string Code, string? DmKey, string Mode) : IRequest<CampaignModeView>;

public sealed class SetModeValidator : AbstractValidator<SetMode>
{
    public SetModeValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.Mode).Must(mode => Enum.TryParse<CampaignMode>(mode, ignoreCase: true, out _))
                            .WithMessage("Mode is Exploration, Encounter or Downtime.");
    }
}

public sealed class SetModeHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<SetMode, CampaignModeView>
{
    public async Task<CampaignModeView> Handle(SetMode command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        CampaignAccess.RequireDm(role, "change the mode");

        campaign.Mode = Enum.Parse<CampaignMode>(command.Mode, ignoreCase: true);
        await db.SaveChangesAsync(ct);

        var view = new CampaignModeView(campaign.Code, campaign.Mode.ToString());
        await broadcaster.ModeChangedAsync(campaign.Code, view, ct);
        return view;
    }
}
