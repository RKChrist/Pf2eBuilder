using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// A campaign has to be created before anything can join it. Creating one on the way past would
/// make a campaign with no DM key, which is a campaign nobody owns.
/// </summary>
public sealed class CampaignNotFoundException(string code) : Exception(
    $"There is no campaign with the code {code}. A campaign has to be created before a character " +
    "can be imported into it, and whoever creates it keeps the DM key.");

/// <summary>Refused for want of the campaign's DM key, which is a 403 and not a 404: the
/// campaign is there and the caller is not the DM of it.</summary>
public sealed class NotTheDmException(string action) : Exception(
    $"Only the DM can {action}, and this request did not carry the campaign's DM key.");

/// <summary>
/// The one way a handler turns a code and a presented key into a campaign and a role. Every
/// command in this folder starts here, so "who is asking" is decided in one place rather than
/// per handler.
/// </summary>
internal static class CampaignAccess
{
    public static async Task<(Campaign Campaign, ViewerRole Role)> LoadAsync(
        ITrackerDbContext db, string code, string? dmKey, CancellationToken ct, bool tracking = true)
    {
        var normalized = CampaignCode.Normalize(code);

        var query = db.Campaigns
            .Include(c => c.Characters)
            .Include(c => c.EffectApplications).ThenInclude(application => application.Targets)
            .Include(c => c.Encounter!).ThenInclude(encounter => encounter.Combatants)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var campaign = await query.SingleOrDefaultAsync(c => c.Code == normalized, ct)
                       ?? throw new CampaignNotFoundException(normalized);

        return (campaign, campaign.RoleFor(dmKey));
    }

    public static void RequireDm(ViewerRole role, string action)
    {
        if (role is not ViewerRole.Dm)
        {
            throw new NotTheDmException(action);
        }
    }

    /// <summary>
    /// Projects once per role, pushes each to its own group, and answers the caller with theirs.
    /// Every encounter command ends here, so "what does each role get told" is decided once.
    /// </summary>
    public static async Task<CampaignView> PublishAsync(
        ICampaignBroadcaster broadcaster, Campaign campaign, ViewerRole role, CancellationToken ct)
    {
        var forDm = CampaignProjection.For(ViewerRole.Dm, campaign);
        var forPlayers = CampaignProjection.For(ViewerRole.Player, campaign);

        await broadcaster.CampaignChangedAsync(campaign.Code, forDm, forPlayers, ct);
        return role is ViewerRole.Dm ? forDm : forPlayers;
    }
}
