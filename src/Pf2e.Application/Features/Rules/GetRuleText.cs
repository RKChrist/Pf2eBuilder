using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Rules;

namespace Pf2e.Application.Features.Rules;

public sealed record GetRuleText(string Id) : IRequest<RuleText?>;

public sealed class GetRuleTextValidator : AbstractValidator<GetRuleText>
{
    public GetRuleTextValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

public sealed class GetRuleTextHandler(IRulesDbContext db, IRuleTextSource source)
    : IRequestHandler<GetRuleText, RuleText?>
{
    /// <summary>Null when the ruleset has no such record. Only an id the seed knows is ever asked
    /// of the source, so this endpoint cannot be used to make the server fetch arbitrary
    /// documents from somebody else's index.</summary>
    public async Task<RuleText?> Handle(GetRuleText query, CancellationToken ct)
    {
        var record = await db.RuleRecords.AsNoTracking()
            .Where(r => r.Id == query.Id)
            .Select(r => new { r.Id, r.SourceUrl })
            .SingleOrDefaultAsync(ct);
        if (record is null)
        {
            return null;
        }

        return await source.FetchAsync(record.Id, ct) switch
        {
            RuleTextAnswer.Found found => new RuleText(RuleProse.Of(found.Markdown), record.SourceUrl, Reached: true),
            RuleTextAnswer.None => new RuleText(null, record.SourceUrl, Reached: true),
            _ => new RuleText(null, record.SourceUrl, Reached: false),
        };
    }
}
