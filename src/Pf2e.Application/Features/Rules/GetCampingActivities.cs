using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// The camping activities, which are the ones a party does while making camp.
/// <para>These come from the ruleset rather than from a table in this codebase, because unlike
/// the buffs they are already there: twenty-three seeded actions carrying the Camping trait, with
/// the requirements they print. Four of those requirements are plain proficiency phrases and are
/// read back into something a sheet can check; the rest stay sentences.</para>
/// </summary>
public sealed record GetCampingActivities : IRequest<IReadOnlyList<CampingActivityView>>;

public sealed class GetCampingActivitiesHandler(IRulesDbContext db)
    : IRequestHandler<GetCampingActivities, IReadOnlyList<CampingActivityView>>
{
    public const string Trait = "Camping";

    public async Task<IReadOnlyList<CampingActivityView>> Handle(
        GetCampingActivities query, CancellationToken ct)
    {
        // Traits are a column of their own, and the provider cannot search inside the list, so
        // the filter happens in memory over the 551 actions rather than over all 25,622 records.
        var actions = await db.RuleRecords.AsNoTracking()
            .Where(r => r.Category == "action")
            .Select(r => new { r.Id, r.Name, r.Traits, r.Mechanics })
            .ToListAsync(ct);

        return
        [
            .. actions
                .Where(action => action.Traits.Contains(Trait, StringComparer.OrdinalIgnoreCase))
                .OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
                .Select(action =>
                {
                    var requires = RuleFields.Text(action.Mechanics, "requirement");
                    var parsed = Requirements.Parse(requires);

                    return new CampingActivityView(
                        action.Id,
                        action.Name,
                        requires,
                        parsed?.Rank.ToString(),
                        parsed?.Skills ?? []);
                }),
        ];
    }
}
