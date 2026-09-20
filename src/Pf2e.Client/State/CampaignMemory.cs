using System.Text.Json;
using Microsoft.JSInterop;

namespace Pf2e.Client.State;

/// <summary>
/// What this browser needs to walk back into the campaign it was already in.
/// <para>The key is the part that matters. It arrives once, from creating the campaign, and is
/// the only thing that separates the GM from a spectator. Holding it in memory alone meant a
/// tab reload, which phones do on their own, silently ended somebody's authority over their own
/// fight for the rest of the session.</para>
/// </summary>
public sealed record RememberedCampaign(string Code, string? DmKey);

/// <summary>
/// The remembered campaign, in this browser's own storage.
/// <para>Every call swallows its failure. Storage is unavailable in a private window and can be
/// switched off entirely, and a table that cannot be remembered still has to work: the fallback
/// is the join form, which is where everybody starts anyway.</para>
/// </summary>
public sealed class CampaignMemory(IJSRuntime js)
{
    const string Key = "pf2e.campaign";

    public async Task<RememberedCampaign?> ReadAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return stored is { Length: > 0 }
                ? JsonSerializer.Deserialize<RememberedCampaign>(stored)
                : null;
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException or JsonException)
        {
            return null;
        }
    }

    public async Task WriteAsync(RememberedCampaign campaign)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(campaign));
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException)
        {
            // Nothing to do about it and nothing worth saying: the table still works, it just
            // will not come back by itself.
        }
    }

    public async Task ForgetAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException)
        {
        }
    }
}
