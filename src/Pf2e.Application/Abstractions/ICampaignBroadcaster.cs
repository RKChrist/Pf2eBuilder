using Pf2e.Contracts.Tracker;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// How a handler tells the rest of the campaign that a character moved. It is implemented over
/// SignalR in the API, which is what keeps handlers free of HTTP. The payload is the whole
/// recomputed sheet, because every viewer is entitled to every player character's numbers and
/// there is therefore nothing to filter.
/// </summary>
public interface ICampaignBroadcaster
{
    Task CharacterChangedAsync(string tableCode, CharacterSheetView sheet, CancellationToken ct);
}
