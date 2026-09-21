using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// The campsite meals the Kingmaker Companion Guide prints, which are the special meals a party
/// can cook without anybody writing a recipe down first.
/// <para>What a meal does is prose the licensing policy withholds, so a seeded meal reaches the
/// table as a name, a level, a rarity and what it takes to cook. A table that wants the words
/// writes its own recipe into the camp book instead.</para>
/// </summary>
public sealed record GetCampsiteMeals : IRequest<IReadOnlyList<CampsiteMealView>>;

public sealed class GetCampsiteMealsHandler(IRulesDbContext db)
    : IRequestHandler<GetCampsiteMeals, IReadOnlyList<CampsiteMealView>>
{
    public const string Category = "campsite-meal";

    public async Task<IReadOnlyList<CampsiteMealView>> Handle(
        GetCampsiteMeals query, CancellationToken ct)
    {
        // Category is a column and the first half of an index, so the provider answers this from
        // the twenty-seven rows themselves. The camping activities query filters on traits, which
        // live inside a serialized list, and pays for an in-memory pass this one does not need.
        var meals = await db.RuleRecords.AsNoTracking()
            .Where(r => r.Category == Category)
            .Select(r => new { r.Id, r.Name, r.Level, r.Rarity, r.Mechanics })
            .ToListAsync(ct);

        return
        [
            .. meals
                .OrderBy(meal => meal.Name, StringComparer.OrdinalIgnoreCase)
                // No seeded meal is missing a level, so a zero here is a record that changed
                // shape rather than a meal a first-level party can cook.
                .Select(meal => new CampsiteMealView(
                    meal.Id,
                    meal.Name,
                    meal.Level ?? 0,
                    meal.Rarity,
                    RuleFields.Text(meal.Mechanics, "requirement"))),
        ];
    }
}
