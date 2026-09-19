using Microsoft.AspNetCore.SignalR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

public sealed class CampaignBroadcaster(IHubContext<CampaignHub> hub) : ICampaignBroadcaster
{
    public Task CharacterChangedAsync(string code, CharacterSheetView sheet, CancellationToken ct) =>
        hub.Clients.Group(CampaignGroups.Everyone(code)).SendAsync("CharacterChanged", sheet, ct);

    public Task ModeChangedAsync(string code, CampaignModeView mode, CancellationToken ct) =>
        hub.Clients.Group(CampaignGroups.Everyone(code)).SendAsync("ModeChanged", mode, ct);

    /// <summary>Two sends and no filtering. The values arrive already projected, and this only
    /// decides which group each is addressed to.</summary>
    public async Task CampaignChangedAsync(
        string code, CampaignView forDm, CampaignView forPlayers, CancellationToken ct)
    {
        await hub.Clients.Group(CampaignGroups.For(code, ViewerRole.Dm))
                 .SendAsync("CampaignChanged", forDm, ct);
        await hub.Clients.Group(CampaignGroups.For(code, ViewerRole.Player))
                 .SendAsync("CampaignChanged", forPlayers, ct);
    }
}
