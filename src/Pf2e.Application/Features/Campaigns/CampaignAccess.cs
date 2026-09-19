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

    /// <summary>
    /// A command that is going to change something, with the state it found on arrival.
    /// <para>The snapshot is taken on the way in and pushed on the way out, by
    /// <see cref="LoadForChangeAsync"/> and <see cref="PublishAsync"/>, which every changing
    /// command already passes through. A rule written down once per handler is a rule the
    /// person adding the eighth handler will not know about.</para>
    /// </summary>
    public readonly record struct CampaignChange(Campaign Campaign, ViewerRole Role, CampaignSnapshot Before);

    public static async Task<CampaignChange> LoadForChangeAsync(
        ITrackerDbContext db, IUndoStack undo, string code, string? dmKey, string label, CancellationToken ct)
    {
        var (campaign, role) = await LoadAsync(db, code, dmKey, ct);
        return new CampaignChange(campaign, role, CampaignSnapshots.Of(campaign, label));
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
    /// <summary>
    /// Publishes, and records the prior state now that the change has actually been saved.
    /// <para>Pushed here and not on the way in, so a command that was refused leaves no undo
    /// step. A snapshot from a request that changed nothing is an undo the DM taps and watches
    /// do nothing, which is worse than no undo at all.</para>
    /// <para><paramref name="projectFrom"/> is for the one command that re-reads after writing
    /// in the database: the snapshot belongs to the campaign it loaded, and the answer belongs
    /// to the one it read back.</para>
    /// </summary>
    public static Task<CampaignView> PublishChangeAsync(
        ICampaignBroadcaster broadcaster,
        IUndoStack undo,
        CampaignChange change,
        CancellationToken ct,
        Campaign? projectFrom = null)
    {
        undo.Push(change.Campaign.Id, change.Before);
        return PublishAsync(broadcaster, projectFrom ?? change.Campaign, change.Role, ct);
    }

    public static async Task<CampaignView> PublishAsync(
        ICampaignBroadcaster broadcaster, Campaign campaign, ViewerRole role, CancellationToken ct)
    {
        var forDm = CampaignProjection.For(ViewerRole.Dm, campaign);
        var forPlayers = CampaignProjection.For(ViewerRole.Player, campaign);

        await broadcaster.CampaignChangedAsync(campaign.Code, forDm, forPlayers, ct);
        return role is ViewerRole.Dm ? forDm : forPlayers;
    }
}
