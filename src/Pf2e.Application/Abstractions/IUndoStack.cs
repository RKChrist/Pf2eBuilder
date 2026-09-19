using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// A bounded stack of prior states per campaign, in memory, lost on restart. design/006 brought
/// undo back as the smaller half of what an earlier decision rejected: damage applied to the
/// wrong combatant mid-fight is the most common mistake at a table, and a durable command log
/// is a different feature that nobody asked for.
/// <para>Lost on restart is a property and not a shortcoming. An undo that survived a deploy
/// would let somebody reverse a turn from a session three weeks ago.</para>
/// </summary>
public interface IUndoStack
{
    void Push(Guid campaignId, CampaignSnapshot snapshot);

    /// <summary>The most recent prior state, or null when there is nothing to undo.</summary>
    CampaignSnapshot? Pop(Guid campaignId);

    /// <summary>Forgets a campaign's history, which is what ending a fight does: there is
    /// nothing in the last encounter left to reverse.</summary>
    void Clear(Guid campaignId);

    int Depth(Guid campaignId);
}
