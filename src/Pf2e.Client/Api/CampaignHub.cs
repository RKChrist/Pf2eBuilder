using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Api;

/// <summary>
/// One connection for the app, holding at most one campaign. A change arrives as the whole
/// recomputed sheet, so a pushed character and a character that came back from a command are
/// the same value and take the same path into the store.
/// </summary>
public sealed class CampaignHub : IAsyncDisposable
{
    readonly HubConnection _connection;

    string? _joined;

    public CampaignHub(IOptions<ApiOptions> options)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(options.Value.BaseUrl), options.Value.HubPath))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<CharacterSheetView>("CharacterChanged", sheet => CharacterChanged?.Invoke(sheet));

        // A reconnection is a new connection to the server, which knows nothing of the group the
        // old one was in, so the campaign has to be rejoined or the page goes quietly stale.
        _connection.Reconnected += async _ =>
        {
            if (_joined is { } code)
            {
                await _connection.InvokeAsync("JoinCampaign", code);
            }
        };
    }

    public event Action<CharacterSheetView>? CharacterChanged;

    public async Task JoinAsync(string code, CancellationToken ct)
    {
        if (_connection.State is HubConnectionState.Disconnected)
        {
            await _connection.StartAsync(ct);
        }

        if (_joined is { } previous && !string.Equals(previous, code, StringComparison.Ordinal))
        {
            await _connection.InvokeAsync("LeaveCampaign", previous, ct);
        }

        await _connection.InvokeAsync("JoinCampaign", code, ct);
        _joined = code;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
