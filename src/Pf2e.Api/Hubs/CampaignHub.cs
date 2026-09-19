using MediatR;
using Microsoft.AspNetCore.SignalR;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

/// <summary>
/// A connection joins its campaign's everyone group and exactly one of the two role groups.
/// The role is decided by the same domain comparison the HTTP side uses, against the campaign
/// the connection named, so a connection cannot talk its way into the DM group.
/// </summary>
public sealed class CampaignHub(ISender sender) : Hub
{
    public async Task JoinCampaign(string code, string? dmKey)
    {
        var campaign = await sender.Send(new GetCampaign(code, dmKey));
        var role = Enum.Parse<ViewerRole>(campaign.Role);

        await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.Everyone(code));
        await Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroups.For(code, role));
    }

    public async Task LeaveCampaign(string code)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.Everyone(code));

        foreach (var role in Enum.GetValues<ViewerRole>())
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroups.For(code, role));
        }
    }
}
