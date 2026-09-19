using System.Text.Json.Nodes;
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

        return record is null ? null : new RuleDetail(RuleSummaries.Of(record), Mechanics(record.Mechanics));
    }

    /// <summary>Key order is the seed's own, which is the only ordering a field this server has
    /// never heard of can be given.</summary>
    static IReadOnlyList<MechanicField> Mechanics(string json) =>
        JsonNode.Parse(json) is JsonObject fields
            ? [.. fields.Select(field => new MechanicField(field.Key, Flatten(field.Value)))]
            : [];

    static IReadOnlyList<string> Flatten(JsonNode? node) => node switch
    {
        JsonArray array => [.. array.Select(Render)],
        null => [],
        _ => [Render(node)],
    };

    static string Render(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue(out string? text) => text,
        _ => node.ToJsonString(),
    };
}
