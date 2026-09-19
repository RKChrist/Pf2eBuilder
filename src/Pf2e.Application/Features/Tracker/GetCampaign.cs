using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Tracker;

public sealed record GetCampaign(string Code) : IRequest<CampaignView>;

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
        var code = CampaignCode.Normalize(query.Code);

        var campaign = await db.Campaigns
            .AsNoTracking()
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        // Anyone with a code is at that campaign, so a code nobody has imported into is a campaign
        // waiting to be started rather than a missing one.
        return campaign is null
            ? new CampaignView(code, false, [])
            : new CampaignView(code, true,
                [.. campaign.Characters
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(SheetViews.Of)]);
    }
}
