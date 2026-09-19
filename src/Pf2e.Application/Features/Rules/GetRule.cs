using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

public sealed record GetRule(string Id) : IRequest<RuleDetail?>;

public sealed class GetRuleValidator : AbstractValidator<GetRule>
{
    public GetRuleValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

public sealed class GetRuleHandler(IRulesDbContext db) : IRequestHandler<GetRule, RuleDetail?>
{
    public async Task<RuleDetail?> Handle(GetRule query, CancellationToken ct)
    {
        var record = await db.RuleRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Id == query.Id, ct);
        if (record is null)
        {
            return null;
        }

        var mechanics = RuleMechanics.Fields(record.Mechanics);
        return new RuleDetail(
            RuleSummaries.Of(record, mechanics),
            mechanics,
            RuleModifiers.Of(record.Mechanics),
            await RuleLinks.Resolve(db, mechanics, ct));
    }
}
