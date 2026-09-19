using Microsoft.AspNetCore.SignalR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Api.Hubs;

public sealed class CampaignBroadcaster(IHubContext<CampaignHub> hub) : ICampaignBroadcaster
{
    public Task CharacterChangedAsync(string code, CharacterSheetView sheet, CancellationToken ct) =>
        hub.Clients.Group(CampaignGroups.Everyone(code)).SendAsync("CharacterChanged", sheet, ct);

    public Task ModeChangedAsync(string code, CampaignModeView mode, CancellationToken ct) =>
        hub.Clients.Group(CampaignGroups.Everyone(code)).SendAsync("ModeChanged", mode, ct);
}
