using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Conditions;

public sealed record GetConditions : IRequest<IReadOnlyList<ConditionSummary>>;

/// <summary>
/// Joins the engine's condition registry to the seeded records. The registry holds the
/// modifiers, because Archives of Nethys publishes a condition's text but not its structured
/// effect. The seed holds the identity and the deep link. A sheet needs both to show a penalty
/// and say where the rule is printed.
/// </summary>
public sealed class GetConditionsHandler(IRulesDbContext db)
    : IRequestHandler<GetConditions, IReadOnlyList<ConditionSummary>>
{
    /// <summary>Registry keys are the seeded name lowercased with spaces as hyphens.</summary>
    public static string SlugOf(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');

    public async Task<IReadOnlyList<ConditionSummary>> Handle(GetConditions query, CancellationToken ct)
    {
        var seeded = await db.RuleRecords
            .AsNoTracking()
            .Where(r => r.Category == "condition")
            .Select(r => new { r.Name, r.SourceUrl })
            .ToListAsync(ct);

        var urlBySlug = seeded
            .GroupBy(r => SlugOf(r.Name))
            .ToDictionary(g => g.Key, g => g.First().SourceUrl, StringComparer.Ordinal);

        return
        [
            .. Domain.Conditions.All.Select(definition => new ConditionSummary(
                definition.Key,
                definition.Name,
                definition.HasValue,
                definition.Verified,
                [.. definition.ModifiersAt(definition.HasValue ? 1 : 0).Select(Summarise)],
                urlBySlug.GetValueOrDefault(definition.Key))),
        ];
    }

    static ModifierSummary Summarise(Modifier m) =>
        new(m.Source, m.Type.ToString(), m.Value, [.. m.Applies.Select(s => s.Describe())]);
}
