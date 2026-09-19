using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// The only operation that brings a campaign into being. It answers with the DM key once and
/// nothing ever sends that key again, which is what makes the creator the DM of what they
/// created. Importing a character into a code nobody created fails instead of starting a
/// campaign whose DM key went to nobody.
/// </summary>
public sealed record CreateCampaign : IRequest<CreatedCampaignView>;

public sealed class CreateCampaignHandler(ITrackerDbContext db)
    : IRequestHandler<CreateCampaign, CreatedCampaignView>
{
    /// <summary>Six characters out of a 32-letter alphabet is a billion codes, so a collision is
    /// a redraw and not a design problem. This caps the loop; it is not an expectation.</summary>
    const int Draws = 5;

    public async Task<CreatedCampaignView> Handle(CreateCampaign command, CancellationToken ct)
    {
        var code = await FreeCodeAsync(ct);
        var campaign = Campaign.Create(code);

        db.Campaigns.Add(campaign);

        // Nothing catches a unique-index violation here. Two creations drawing the same code in
        // the same millisecond would have to beat a billion-to-one draw, and swallowing the
        // exception to retry would mean saving a context that still holds the rejected row.
        await db.SaveChangesAsync(ct);

        return new CreatedCampaignView(campaign.Code, campaign.DmKey, campaign.Mode.ToString());
    }

    async Task<string> FreeCodeAsync(CancellationToken ct)
    {
        for (var draw = 0; draw < Draws; draw++)
        {
            var code = CampaignCode.Draw();
            if (!await db.Campaigns.AnyAsync(c => c.Code == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            $"Every one of {Draws} codes drawn is already a campaign, which should not happen.");
    }
}
