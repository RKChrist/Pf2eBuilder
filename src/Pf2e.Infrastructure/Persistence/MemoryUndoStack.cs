using System.Collections.Concurrent;
using Pf2e.Application.Abstractions;
using Pf2e.Domain.Tracking;

namespace Pf2e.Infrastructure.Persistence;

/// <summary>
/// The whole of undo's storage. A singleton dictionary of bounded stacks, which is why an undo
/// does not survive a restart and why nothing here is a migration.
/// <para>Both caps exist so that a process left running for a month cannot grow without bound:
/// each campaign keeps a fixed number of prior states, and a fixed number of campaigns keep any
/// at all. Passing either cap drops the oldest, which is the state nobody is going to reach
/// back to anyway.</para>
/// </summary>
public sealed class MemoryUndoStack : IUndoStack
{
    /// <summary>Deep enough for the mistakes a DM actually notices, which are the last few.</summary>
    public const int PerCampaign = 20;

    public const int Campaigns = 64;

    readonly ConcurrentDictionary<Guid, Entry> _stacks = new();

    long _clock;

    public void Push(Guid campaignId, CampaignSnapshot snapshot)
    {
        var entry = _stacks.GetOrAdd(campaignId, _ => new Entry());

        lock (entry.Gate)
        {
            entry.Snapshots.Add(snapshot);
            if (entry.Snapshots.Count > PerCampaign)
            {
                entry.Snapshots.RemoveAt(0);
            }

            entry.Touched = Interlocked.Increment(ref _clock);
        }

        Evict();
    }

    public CampaignSnapshot? Pop(Guid campaignId)
    {
        if (!_stacks.TryGetValue(campaignId, out var entry))
        {
            return null;
        }

        lock (entry.Gate)
        {
            if (entry.Snapshots.Count == 0)
            {
                return null;
            }

            var last = entry.Snapshots[^1];
            entry.Snapshots.RemoveAt(entry.Snapshots.Count - 1);
            entry.Touched = Interlocked.Increment(ref _clock);
            return last;
        }
    }

    public void Clear(Guid campaignId) => _stacks.TryRemove(campaignId, out _);

    public int Depth(Guid campaignId)
    {
        if (!_stacks.TryGetValue(campaignId, out var entry))
        {
            return 0;
        }

        lock (entry.Gate)
        {
            return entry.Snapshots.Count;
        }
    }

    void Evict()
    {
        while (_stacks.Count > Campaigns)
        {
            var oldest = _stacks.OrderBy(pair => pair.Value.Touched).FirstOrDefault();
            if (oldest.Key == Guid.Empty || !_stacks.TryRemove(oldest.Key, out _))
            {
                return;
            }
        }
    }

    sealed class Entry
    {
        public Lock Gate { get; } = new();

        public List<CampaignSnapshot> Snapshots { get; } = [];

        public long Touched { get; set; }
    }
}
