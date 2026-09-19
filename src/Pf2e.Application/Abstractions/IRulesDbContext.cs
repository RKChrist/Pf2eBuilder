using Microsoft.EntityFrameworkCore;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Abstractions;

/// <summary>
/// What handlers are allowed to see of persistence. There is deliberately no repository per
/// entity: a repository wrapping a DbSet is a layer with one implementation that only makes
/// queries harder to read. This interface exists so handlers depend on an abstraction rather
/// than on the concrete context, and so tests can substitute one.
/// </summary>
public interface IRulesDbContext
{
    DbSet<RuleRecord> RuleRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
