using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>With no filter this is the size of every category; with a trait it is everywhere
/// that trait is used.</summary>
public sealed record CountRules(string? Name = null, string? Trait = null) : IRequest<RuleCounts>;

public sealed class CountRulesValidator : AbstractValidator<CountRules>
{
    public CountRulesValidator()
    {
        RuleFor(q => q.Trait).NotEmpty()
                             .When(q => q.Trait is not null)
                             .WithMessage("Trait must be a name, not an empty string.");
    }
}

public sealed class CountRulesHandler(IRulesDbContext db) : IRequestHandler<CountRules, RuleCounts>
{
    public async Task<RuleCounts> Handle(CountRules query, CancellationToken ct)
    {
        // The same correction the list applies, so the counts beside a list never disagree with it.
        var records = db.RuleRecords.AsNoTracking()
            .NameContains(await RuleSpelling.CorrectAsync(db, query.Name, ct));

        List<CategoryCount> counts;
        if (query.Trait is { Length: > 0 } trait)
        {
            // Unfiltered, every one of the 21,000 records is a candidate, so only the two
            // columns the count needs are read and the mechanics document is never touched.
            var rows = await records.Select(r => new { r.Category, r.Level, r.Traits }).ToListAsync(ct);
            counts =
            [
                .. rows.Where(r => RuleFilters.HasTrait(r.Traits, trait))
                       .GroupBy(r => r.Category)
                       .Select(g => new CategoryCount(g.Key, g.Count(), g.Min(r => r.Level), g.Max(r => r.Level))),
            ];
        }
        else
        {
            var grouped = await records
                .GroupBy(r => r.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Lowest = g.Min(r => r.Level), Highest = g.Max(r => r.Level) })
                .ToListAsync(ct);
            counts = [.. grouped.Select(g => new CategoryCount(g.Category, g.Count, g.Lowest, g.Highest))];
        }

        return new RuleCounts(
            counts.Sum(c => c.Count),
            [.. counts.OrderByDescending(c => c.Count).ThenBy(c => c.Category, StringComparer.Ordinal)]);
    }
}
