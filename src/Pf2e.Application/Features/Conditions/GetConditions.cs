using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Conditions;

public sealed record GetConditions : IRequest<IReadOnlyList<ConditionSummary>>;

/// <summary>
/// Joins the engine's effect registry to the seeded records. The registry holds the modifiers,
/// because Archives of Nethys publishes a rule's text but not its structured effect. The seed
/// holds the identity and the deep link. A sheet needs both to show a number and say where the
/// rule it came from is printed.
/// <para>The join spans every category the registry names, not only conditions, because Raise a
/// Shield is an action and bless is a spell and the table applies all three the same way.</para>
/// </summary>
public sealed class GetConditionsHandler(IRulesDbContext db)
    : IRequestHandler<GetConditions, IReadOnlyList<ConditionSummary>>
{
    /// <summary>Registry keys are the seeded name lowercased with spaces as hyphens.</summary>
    public static string SlugOf(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');

    public async Task<IReadOnlyList<ConditionSummary>> Handle(GetConditions query, CancellationToken ct)
    {
        var categories = Effects.All.Select(e => e.RuleCategory).Distinct().ToList();

        var seeded = await db.RuleRecords
            .AsNoTracking()
            .Where(r => categories.Contains(r.Category))
            .Select(r => new { r.Category, r.Name, r.SourceUrl })
            .ToListAsync(ct);

        // Keyed by category as well as slug: "shield" is a spell and also a piece of equipment,
        // and the spell's +1 to armour class must not link to a steel shield's shopping entry.
        var urls = seeded
            .GroupBy(r => (r.Category, Slug: SlugOf(r.Name)))
            .ToDictionary(g => g.Key, g => g.First().SourceUrl);

        return
        [
            .. Effects.All.Select(definition => new ConditionSummary(
                definition.Key,
                definition.Name,
                definition.Kind.ToString(),
                definition.HasValue,
                definition.Verified,
                [.. definition.ModifiersAt(definition.HasValue ? 1 : 0).Select(Summarise)],
                urls.GetValueOrDefault((definition.RuleCategory, SlugOf(definition.RuleName ?? definition.Name))))),
        ];
    }

    static ModifierSummary Summarise(Modifier m) =>
        new(m.Source, m.Type.ToString(), m.Value, [.. m.Applies.Select(s => s.Describe())]);
}
