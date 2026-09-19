namespace Pf2e.Domain;

/// <summary>
/// Which of the game's three modes the campaign is in. It lives on the campaign rather than in
/// each client's local state, because a group where half the phones are on a different screen
/// from the other half is worse than no app at all.
/// </summary>
public enum CampaignMode
{
    /// <summary>The resting state, and where a campaign starts.</summary>
    Exploration,

    /// <summary>Entered by rolling initiative and left when the DM ends the encounter.</summary>
    Encounter,

    /// <summary>Entered deliberately.</summary>
    Downtime,
}
