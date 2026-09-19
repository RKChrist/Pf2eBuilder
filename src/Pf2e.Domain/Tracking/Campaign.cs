namespace Pf2e.Domain.Tracking;

/// <summary>
/// The thing the group plays: a roster, the mode it is currently in, and the two secrets that
/// separate the DM from everyone else. A campaign is created explicitly, which is what makes
/// "whoever creates it keeps the DM key" true rather than nearly true.
/// </summary>
public sealed class Campaign
{
    public required Guid Id { get; init; }

    /// <summary>What everyone types. Short, because it is read out across a table.</summary>
    public required string Code { get; init; }

    /// <summary>What the creator keeps. Never in a payload except the one that answers the
    /// request that created the campaign.</summary>
    public required string DmKey { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public CampaignMode Mode { get; set; } = CampaignMode.Exploration;

    public List<TrackedCharacter> Characters { get; init; } = [];

    public static Campaign Create(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = CampaignCode.Normalize(code),
        DmKey = Domain.DmKey.Draw(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    public ViewerRole RoleFor(string? presentedKey) => Domain.DmKey.RoleFor(DmKey, presentedKey);
}
