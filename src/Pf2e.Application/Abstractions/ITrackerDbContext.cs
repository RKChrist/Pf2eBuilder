using Microsoft.EntityFrameworkCore;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// A second context abstraction rather than a wider <see cref="IRulesDbContext"/>, so a rules
/// handler cannot reach the tracker tables. One context implements both.
/// </summary>
public interface ITrackerDbContext
{
    DbSet<Campaign> Campaigns { get; }

    DbSet<TrackedCharacter> Characters { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
