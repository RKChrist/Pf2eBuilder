using System.Text.Json;
using Microsoft.JSInterop;

namespace Pf2e.Client.State;

/// <summary>
/// What this browser needs to walk back into a campaign it was already in.
/// <para>The key is the part that matters. It arrives once, from creating the campaign, and is
/// the only thing that separates the GM from a spectator. Holding it in memory alone meant a
/// tab reload, which phones do on their own, silently ended somebody's authority over their own
/// fight for the rest of the session.</para>
/// <para><paramref name="LastOpened"/> is what orders the list, so the table played last night is
/// the one at the top rather than the one whose code happens to sort first.</para>
/// </summary>
public sealed record RememberedCampaign(string Code, string? DmKey, DateTimeOffset LastOpened);

/// <summary>
/// The campaigns this browser has been in, newest first, in its own storage.
/// <para>A list rather than one campaign, because a GM runs two tables and the single slot this
/// replaces was overwritten by whichever one they opened last. The one it overwrote was gone for
/// good: the DM key is shown nowhere and exists nowhere else, so losing it was losing the
/// campaign.</para>
/// <para>Every call swallows its failure. Storage is unavailable in a private window and can be
/// switched off entirely, and a table that cannot be remembered still has to work: the fallback
/// is the join form, which is where everybody starts anyway.</para>
/// </summary>
public sealed class CampaignMemory(IJSRuntime js, TimeProvider clock)
{
    const string Key = "pf2e.campaigns";

    /// <summary>The single campaign this browser used to hold. Read once more, folded into the
    /// list and deleted, because somebody with a campaign open right now is mid-session.</summary>
    const string OldKey = "pf2e.campaign";

    /// <summary>A convenience list, not a record of anything. Uncapped it grows for as long as
    /// the browser lives, and nobody is scrolling to the campaign they played in March.</summary>
    const int Most = 8;

    public async Task<IReadOnlyList<RememberedCampaign>> ReadAllAsync()
    {
        var kept = await LoadAsync();

        if (await CarriedOverAsync() is not { } carried)
        {
            return kept;
        }

        var folded = Folded(kept, carried);
        await SaveAsync(folded);
        await DropAsync(OldKey);
        return folded;
    }

    /// <summary>The one path in. Joining and creating both end here, so neither can be the one
    /// that forgets.</summary>
    public async Task<IReadOnlyList<RememberedCampaign>> RememberAsync(string code, string? dmKey)
    {
        var kept = await ReadAllAsync();

        // The key this browser already holds survives an open that arrives without one. A GM who
        // leaves their own table and then types its code back in is still the GM, and forgetting
        // is the only thing in this app that drops a key.
        var known = kept.FirstOrDefault(campaign => Same(campaign.Code, code));
        var opened = new RememberedCampaign(code, dmKey ?? known?.DmKey, clock.GetUtcNow());

        var list = Folded(kept, opened);
        await SaveAsync(list);
        return list;
    }

    public async Task<IReadOnlyList<RememberedCampaign>> ForgetAsync(string code)
    {
        var kept = await ReadAllAsync();
        var list = kept.Where(campaign => !Same(campaign.Code, code)).ToList();
        await SaveAsync(list);
        return list;
    }

    static bool Same(string code, string other) =>
        string.Equals(code, other, StringComparison.OrdinalIgnoreCase);

    static List<RememberedCampaign> Folded(
        IReadOnlyList<RememberedCampaign> kept, RememberedCampaign head)
    {
        var list = new List<RememberedCampaign>(Most) { head };
        list.AddRange(kept.Where(campaign => !Same(campaign.Code, head.Code)));
        return list.Count > Most ? list[..Most] : list;
    }

    async Task<List<RememberedCampaign>> LoadAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            var read = stored is { Length: > 0 }
                ? JsonSerializer.Deserialize<List<RememberedCampaign>>(stored)
                : null;

            return read?.Where(campaign => campaign is { Code.Length: > 0 }).ToList() ?? [];
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException or JsonException)
        {
            return [];
        }
    }

    /// <summary>The campaign this browser was in before there was a list, stamped as opened now
    /// because it is the one the person is looking at.</summary>
    async Task<RememberedCampaign?> CarriedOverAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", OldKey);
            var old = stored is { Length: > 0 }
                ? JsonSerializer.Deserialize<RememberedCampaign>(stored)
                : null;

            return old is { Code.Length: > 0 } ? old with { LastOpened = clock.GetUtcNow() } : null;
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException or JsonException)
        {
            return null;
        }
    }

    async Task SaveAsync(IReadOnlyList<RememberedCampaign> campaigns)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(campaigns));
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException)
        {
            // Nothing to do about it and nothing worth saying: the table still works, it just
            // will not come back by itself.
        }
    }

    async Task DropAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException)
        {
        }
    }
}
