using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// The one function that turns a campaign and the role of whoever is asking into the value that
/// role is allowed to have. Every read and every command answers through here.
/// <para>One function and not a filter per handler. A filter per handler is a rule written down
/// as many times as there are handlers, and the second place is the one that leaks.</para>
/// </summary>
internal static class CampaignProjection
{
    public static CampaignView For(ViewerRole role, Campaign campaign) => new(
        campaign.Code,
        campaign.Mode.ToString(),
        role.ToString(),
        [.. campaign.Characters
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(character => SheetViews.Of(character, campaign.EffectApplications))]);
}
