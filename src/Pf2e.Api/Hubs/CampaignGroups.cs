using Pf2e.Domain;

namespace Pf2e.Api.Hubs;

/// <summary>
/// Three group names per campaign: one the DM's connections are in, one the players' are in,
/// and one that is both. A push whose content differs by role is sent to the two role groups
/// with the values the projection already produced, so a player connection is never in a group
/// that is sent a DM payload.
/// </summary>
public static class CampaignGroups
{
    public static string Everyone(string code) => CampaignCode.Normalize(code);

    public static string For(string code, ViewerRole role) =>
        $"{CampaignCode.Normalize(code)}:{role}";
}
