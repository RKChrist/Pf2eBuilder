using Microsoft.AspNetCore.SignalR;
using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

/// <summary>
/// A connection joins the group named by its campaign code and nothing else happens here. Every
/// viewer is entitled to every player character's numbers, so there is nothing to filter and no
/// projection to get wrong.
/// </summary>
public sealed class CampaignHub : Hub
{
    public Task JoinCampaign(string code) =>
        Groups.AddToGroupAsync(Context.ConnectionId, CampaignCode.Normalize(code));

    public Task LeaveCampaign(string code) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignCode.Normalize(code));
}
