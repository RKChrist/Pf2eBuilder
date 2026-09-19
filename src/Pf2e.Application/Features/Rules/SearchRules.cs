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

public sealed class SearchRulesHandler(IRulesDbContext db) : IRequestHandler<SearchRules, RuleSearchResult>
{
    public async Task<RuleSearchResult> Handle(SearchRules query, CancellationToken ct)
    {
        IQueryable<RuleRecord> records = db.RuleRecords.AsNoTracking();

        if (query.Category is { Length: > 0 } category)
        {
            records = records.Where(r => r.Category == category);
        }

        if (query.Name is { Length: > 0 } name)
        {
            records = records.Where(r => EF.Functions.Like(r.Name, $"%{name}%"));
        }

        if (query.MinLevel is int min)
        {
            records = records.Where(r => r.Level >= min);
        }

        if (query.MaxLevel is int max)
        {
            records = records.Where(r => r.Level <= max);
        }

        // Traits are a JSON array behind a value converter, so no provider can translate this
        // into SQL. Filtering after materialising is correct rather than merely convenient: the
        // largest category is 6,568 rows, and paging before the filter would drop matches.
        if (query.Trait is { Length: > 0 } trait)
        {
            var matched = (await records.ToListAsync(ct))
                .Where(r => r.Traits.Contains(trait, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Page(matched, matched.Count, query);
        }

        var total = await records.CountAsync(ct);
        var page = await records
            .OrderBy(r => r.Name).ThenBy(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new RuleSearchResult([.. page.Select(RuleSummaries.Of)], total, query.Page, query.PageSize);
    }

    static RuleSearchResult Page(List<RuleRecord> all, int total, SearchRules query)
    {
        var items = all
            .OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(RuleSummaries.Of)
            .ToList();

        return new RuleSearchResult(items, total, query.Page, query.PageSize);
    }
}
