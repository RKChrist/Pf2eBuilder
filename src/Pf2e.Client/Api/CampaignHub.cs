using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Api;

/// <summary>
/// One connection for the app, holding at most one campaign. A change arrives as the whole
/// recomputed value, so a push and an answer that came back from a command are the same value
/// and take the same path into the store.
/// </summary>
public sealed class CampaignHub : IAsyncDisposable
{
    readonly HubConnection _connection;

    string? _joined;
    string? _dmKey;

    public CampaignHub(IOptions<ApiOptions> options)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(options.Value.BaseUrl), options.Value.HubPath))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<CharacterSheetView>("CharacterChanged", sheet => CharacterChanged?.Invoke(sheet));
        _connection.On<CampaignModeView>("ModeChanged", mode => ModeChanged?.Invoke(mode));

        // Already projected for this connection's role by the time it arrives, so there is
        // nothing for the client to filter and nothing for it to get wrong.
        _connection.On<CampaignView>("CampaignChanged", campaign => CampaignChanged?.Invoke(campaign));

        // A reconnection is a new connection to the server, which knows nothing of the groups the
        // old one was in, so the campaign has to be rejoined or the page goes quietly stale. The
        // key goes with it, because the role is decided per connection.
        _connection.Reconnected += async _ =>
        {
            if (_joined is { } code)
            {
                await _connection.InvokeAsync("JoinCampaign", code, _dmKey);
            }
        };
    }

    public event Action<CharacterSheetView>? CharacterChanged;

    public event Action<CampaignModeView>? ModeChanged;

    public event Action<CampaignView>? CampaignChanged;

    public async Task JoinAsync(string code, string? dmKey, CancellationToken ct)
    {
        if (_connection.State is HubConnectionState.Disconnected)
        {
            await _connection.StartAsync(ct);
        }

        if (_joined is { } previous && !string.Equals(previous, code, StringComparison.Ordinal))
        {
            await _connection.InvokeAsync("LeaveCampaign", previous, ct);
        }

        await _connection.InvokeAsync("JoinCampaign", code, dmKey, ct);
        _joined = code;
        _dmKey = dmKey;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
