using Microsoft.EntityFrameworkCore;
using Pf2e.Domain.Accounts;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// A third context abstraction rather than a wider <see cref="ITrackerDbContext"/>, so a
/// campaign handler cannot reach the accounts table and an account handler cannot reach a
/// party. One context implements all three.
/// </summary>
public interface IAccountsDbContext
{
    DbSet<Account> Accounts { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
