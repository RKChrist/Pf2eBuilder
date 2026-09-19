using Microsoft.AspNetCore.SignalR;
using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

/// <summary>
/// A connection joins the group named by its table code and nothing else happens here. Every
/// viewer is entitled to every player character's numbers, so there is nothing to filter and no
/// projection to get wrong.
/// </summary>
public sealed class TableHub : Hub
{
    public Task JoinTable(string code) =>
        Groups.AddToGroupAsync(Context.ConnectionId, TableCode.Normalize(code));

    public Task LeaveTable(string code) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TableCode.Normalize(code));
}
