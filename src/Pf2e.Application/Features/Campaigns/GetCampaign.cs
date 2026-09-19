using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

public sealed record GetCampaign(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class GetCampaignValidator : AbstractValidator<GetCampaign>
{
    public GetCampaignValidator()
    {
        RuleFor(q => q.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
    }
}

public sealed class GetCampaignHandler(ITrackerDbContext db) : IRequestHandler<GetCampaign, CampaignView>
{
    public async Task<CampaignView> Handle(GetCampaign query, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadAsync(db, query.Code, query.DmKey, ct, tracking: false);

        return CampaignProjection.For(role, campaign);
    }
}
