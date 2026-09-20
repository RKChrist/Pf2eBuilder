using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

public sealed record SearchRules(
    string? Category = null,
    string? Name = null,
    int? MinLevel = null,
    int? MaxLevel = null,
    string? Trait = null,
    int Page = 1,
    int PageSize = 50) : IRequest<RuleSearchResult>;

public sealed class SearchRulesValidator : AbstractValidator<SearchRules>
{
    public SearchRulesValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.MinLevel).InclusiveBetween(-1, 30).When(q => q.MinLevel.HasValue);
        RuleFor(q => q.MaxLevel).InclusiveBetween(-1, 30).When(q => q.MaxLevel.HasValue);
        RuleFor(q => q).Must(q => q.MinLevel is null || q.MaxLevel is null || q.MinLevel <= q.MaxLevel)
                       .WithMessage("MinLevel must not exceed MaxLevel.");
        RuleFor(q => q.Trait).NotEmpty()
                             .When(q => q.Trait is not null)
                             .WithMessage("Trait must be a name, not an empty string.");
    }
}

/// <summary>
/// A name search ranks the exact name first, then names that start with the query, then names
/// that merely contain it, alphabetically within each. Typing "shield" has to find Shield before
/// Reinforced Shield Block, or search looks broken on the most common thing anyone asks it.
/// </summary>
public sealed class SearchRulesHandler(IRulesDbContext db) : IRequestHandler<SearchRules, RuleSearchResult>
{
    public async Task<RuleSearchResult> Handle(SearchRules query, CancellationToken ct)
    {
        var name = await RuleSpelling.CorrectAsync(db, query.Name, ct);
        var searchedFor = name == query.Name ? null : name;

        var records = db.RuleRecords.AsNoTracking()
            .Called(db.RuleAliases.AsNoTracking(), name)
            .InCategory(query.Category)
            .LevelBetween(query.MinLevel, query.MaxLevel);

        if (query.Trait is { Length: > 0 } trait)
        {
            var byTrait = await ByTrait(records, trait, query, ct);
            return byTrait with { SearchedFor = searchedFor, FormerNames = await FormerNames(byTrait.Items, name, ct) };
        }

        var total = await records.CountAsync(ct);
        var page = await Ranked(records, name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = page.Select(RuleSummaries.Of).ToList();
        return new RuleSearchResult(
            items, total, query.Page, query.PageSize, searchedFor, await FormerNames(items, name, ct));
    }

    /// <summary>What each record on this page used to be called, for the ones that are here
    /// because of what they used to be called. Null when none are, which is nearly always.</summary>
    async Task<IReadOnlyDictionary<string, string>?> FormerNames(
        IReadOnlyList<RuleSummary> items, string? name, CancellationToken ct)
    {
        var words = RuleFilters.Words(name);
        var found = items
            .Where(item => !words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.Id)
            .ToList();
        if (found.Count == 0)
        {
            return null;
        }

        var was = await db.RuleAliases.AsNoTracking()
            .WasContains(name)
            .Where(alias => found.Contains(alias.NowId))
            .ToListAsync(ct);

        return was.GroupBy(alias => alias.NowId)
                  .ToDictionary(group => group.Key, group => group.OrderBy(alias => alias.Was.Length).First().Was);
    }

    static IOrderedQueryable<RuleRecord> Ranked(IQueryable<RuleRecord> records, string? name)
    {
        if (name is not { Length: > 0 })
        {
            return records.OrderBy(r => r.Name).ThenBy(r => r.Id);
        }

        // "raise shield" is not how Raise a Shield starts, so a name that opens with the first
        // word typed ranks beside one that opens with the whole phrase.
        var lowered = name.ToLowerInvariant();
        var first = RuleFilters.Words(lowered)[0];
        return records
            .OrderBy(r => r.Name.ToLower() == lowered ? 0 : r.Name.ToLower().StartsWith(first) ? 1 : 2)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.Id);
    }

    /// <summary>
    /// Traits are a JSON array behind a value converter, so no provider can translate the filter
    /// into SQL, and paging before it would drop matches. Every candidate is read, but only the
    /// columns the filter and the order need; the mechanics document is read for the one page
    /// that is returned.
    /// </summary>
    async Task<RuleSearchResult> ByTrait(IQueryable<RuleRecord> records, string trait, SearchRules query, CancellationToken ct)
    {
        var candidates = await records.Select(r => new { r.Id, r.Name, r.Traits }).ToListAsync(ct);
        var matched = candidates.Where(r => RuleFilters.HasTrait(r.Traits, trait)).ToList();

        var pageIds = matched
            .OrderBy(r => Rank(r.Name, query.Name))
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => r.Id)
            .ToList();

        var rows = await db.RuleRecords.AsNoTracking()
            .Where(r => pageIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        return new RuleSearchResult(
            [.. pageIds.Select(id => RuleSummaries.Of(rows[id]))], matched.Count, query.Page, query.PageSize);
    }

    /// <summary>The in-memory twin of <see cref="Ranked"/>, for the one path SQL cannot filter.</summary>
    static int Rank(string candidate, string? name) =>
        name is not { Length: > 0 } ? 0
        : candidate.Equals(name, StringComparison.OrdinalIgnoreCase) ? 0
        : candidate.StartsWith(name, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;
}
