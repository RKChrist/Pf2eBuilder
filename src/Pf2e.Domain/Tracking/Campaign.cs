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

    /// <summary>
    /// How long the party has been at this since somebody started counting, in minutes. The
    /// camp panel adds to it and nothing subtracts, because it answers "how long have we been
    /// here?" and that question has never had a smaller answer than the last time it was asked.
    /// </summary>
    public int ElapsedMinutes { get; set; }

    /// <summary>
    /// Which downtime day the party is on, counting from one. Separate from
    /// <see cref="ElapsedMinutes"/> because downtime is counted in days and camp in minutes,
    /// and a single number in one unit would make one of the two screens do arithmetic to
    /// answer the question it exists for.
    /// </summary>
    public int Day { get; set; } = 1;

    public List<TrackedCharacter> Characters { get; init; } = [];

    /// <summary>Every effect in play, on characters and on monsters alike. They hang off the
    /// campaign rather than off a creature because one application can reach several.</summary>
    public List<EffectApplication> EffectApplications { get; init; } = [];

    /// <summary>At most one, and null until somebody adds the first combatant.</summary>
    public Encounter? Encounter { get; set; }

    public static Campaign Create(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = CampaignCode.Normalize(code),
        DmKey = Domain.DmKey.Draw(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    public ViewerRole RoleFor(string? presentedKey) => Domain.DmKey.RoleFor(DmKey, presentedKey);
}
