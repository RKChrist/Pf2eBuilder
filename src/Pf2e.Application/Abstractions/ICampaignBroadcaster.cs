using Pf2e.Contracts.Tracker;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// How a handler tells the rest of the campaign that something moved. It is implemented over
/// SignalR in the API, which is what keeps handlers free of HTTP.
/// <para>A character sheet and a mode go to everyone, because every viewer is entitled to every
/// player character's numbers and to the mode the group is in. Anything that differs by role
/// takes a pair of already-projected values and this interface only routes them, so nothing
/// here ever decides what a player may see.</para>
/// </summary>
public interface ICampaignBroadcaster
{
    Task CharacterChangedAsync(string code, CharacterSheetView sheet, CancellationToken ct);

    Task ModeChangedAsync(string code, CampaignModeView mode, CancellationToken ct);

    /// <summary>
    /// Anything an encounter changed. It takes the two values the projection already produced
    /// and only chooses which group each goes to, so no decision about what a player may see is
    /// taken here. A DM payload has nowhere to go but the DM group.
    /// </summary>
    Task CampaignChangedAsync(string code, CampaignView forDm, CampaignView forPlayers, CancellationToken ct);
}
