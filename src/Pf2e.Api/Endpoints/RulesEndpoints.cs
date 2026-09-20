using MediatR;
using Pf2e.Application.Features.Conditions;
using Pf2e.Application.Features.Rules;

namespace Pf2e.Api.Endpoints;

/// <summary>
/// Endpoints translate HTTP into a request and nothing else. No querying, no mapping, no rules.
/// If an endpoint grows a second statement, the logic belongs in a handler.
/// </summary>
public static class RulesEndpoints
{
    public static IEndpointRouteBuilder MapRules(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rules", (ISender sender, [AsParameters] SearchRules query, CancellationToken ct) =>
            sender.Send(query, ct));

        app.MapGet("/rules/counts", (ISender sender, [AsParameters] CountRules query, CancellationToken ct) =>
            sender.Send(query, ct));

        app.MapGet("/rules/traits", (ISender sender, [AsParameters] CountTraits query, CancellationToken ct) =>
            sender.Send(query, ct));

        app.MapGet("/rules/{id}", async (ISender sender, string id, CancellationToken ct) =>
            await sender.Send(new GetRule(id), ct) is { } rule ? Results.Ok(rule) : Results.NotFound());

        app.MapGet("/rules/{id}/text", async (ISender sender, string id, CancellationToken ct) =>
            await sender.Send(new GetRuleText(id), ct) is { } text ? Results.Ok(text) : Results.NotFound());

        app.MapGet("/conditions", (ISender sender, CancellationToken ct) =>
            sender.Send(new GetConditions(), ct));

        return app;
    }
}
