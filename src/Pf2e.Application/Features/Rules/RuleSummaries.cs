using Pf2e.Contracts.Rules;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

internal static class RuleSummaries
{
    public static RuleSummary Of(RuleRecord r) =>
        new(r.Id, r.Category, r.Name, r.Level, r.Rarity, r.PrimarySource, r.Traits, r.SourceUrl);
}
