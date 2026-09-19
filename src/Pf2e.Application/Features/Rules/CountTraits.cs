using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

/// <summary>Every trait in one category under a name and level filter, with how many records
/// carry it. The trait filter itself is not applied: these are what a reader picks one from.</summary>
public sealed record CountTraits(string Category, string? Name = null, int? MinLevel = null, int? MaxLevel = null)
    : IRequest<TraitCounts>;

public sealed class CountTraitsValidator : AbstractValidator<CountTraits>
{
    public CountTraitsValidator()
    {
        RuleFor(q => q.Category).NotEmpty();
        RuleFor(q => q).Must(q => q.MinLevel is null || q.MaxLevel is null || q.MinLevel <= q.MaxLevel)
                       .WithMessage("MinLevel must not exceed MaxLevel.");
    }
}

public sealed class CountTraitsHandler(IRulesDbContext db) : IRequestHandler<CountTraits, TraitCounts>
{
    public async Task<TraitCounts> Handle(CountTraits query, CancellationToken ct)
    {
        // Traits are a JSON array behind a value converter, so the counting happens here; only
        // the one column it needs is read.
        var lists = await db.RuleRecords.AsNoTracking()
            .InCategory(query.Category)
            .NameContains(query.Name)
            .LevelBetween(query.MinLevel, query.MaxLevel)
            .Select(r => r.Traits)
            .ToListAsync(ct);

        var groups = await Groups(ct);

        return new TraitCounts(
        [
            .. lists.SelectMany(traits => traits.Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(trait => trait, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TraitCount(Spelling(group), group.Count(), groups.GetValueOrDefault(group.Key)))
                .OrderByDescending(trait => trait.Count)
                .ThenBy(trait => trait.Trait, StringComparer.OrdinalIgnoreCase),
        ]);
    }

    /// <summary>A trait in several groups, such as Elf in Ancestry and Weapon, is filed under the
    /// first of these it has, and otherwise under the first its record lists.</summary>
    static readonly string[] GroupPriority = ["Class", "Ancestry", "Feat", "Tradition", "Rarity"];

    async Task<Dictionary<string, string>> Groups(CancellationToken ct)
    {
        var traits = await db.RuleRecords.AsNoTracking()
            .Where(r => r.Category == "trait")
            .Select(r => new { r.Name, r.Mechanics })
            .ToListAsync(ct);

        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trait in traits)
        {
            var listed = RuleMechanics.Fields(trait.Mechanics).FirstOrDefault(field => field.Key == "trait_group")?.Values ?? [];
            if ((GroupPriority.FirstOrDefault(listed.Contains) ?? listed.FirstOrDefault()) is { } group)
            {
                groups.TryAdd(trait.Name, group);
            }
        }

        return groups;
    }

    /// <summary>The spelling most records use, so one stray lower-case "fire" does not rename
    /// the trait for everyone.</summary>
    static string Spelling(IGrouping<string, string> group) =>
        group.GroupBy(trait => trait, StringComparer.Ordinal)
             .OrderByDescending(spelling => spelling.Count())
             .ThenBy(spelling => spelling.Key, StringComparer.Ordinal)
             .First().Key;
}
