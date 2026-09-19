using Microsoft.AspNetCore.SignalR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

public sealed class CampaignBroadcaster(IHubContext<CampaignHub> hub) : ICampaignBroadcaster
{
    public Task CharacterChangedAsync(string tableCode, CharacterSheetView sheet, CancellationToken ct) =>
        hub.Clients.Group(CampaignCode.Normalize(tableCode)).SendAsync("CharacterChanged", sheet, ct);
}
