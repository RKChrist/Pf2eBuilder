using MediatR;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Api.Endpoints;

public static class CampaignEndpoints
{
    /// <summary>
    /// The DM key travels in a header rather than in the path, because a URL ends up in every
    /// access log between the phone and the process and a bearer secret should not.
    /// </summary>
    public const string DmKeyHeader = "X-DM-Key";

    public static IEndpointRouteBuilder MapCampaigns(this IEndpointRouteBuilder app)
    {
        app.MapPost("/campaigns", (ISender sender, CancellationToken ct) =>
            sender.Send(new CreateCampaign(), ct));

        app.MapGet("/campaigns/{code}", (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
            sender.Send(new GetCampaign(code, DmKeyOf(request)), ct));

        app.MapPost("/campaigns/{code}/mode",
            (ISender sender, HttpRequest request, string code, SetModeRequest body, CancellationToken ct) =>
                sender.Send(new SetMode(code, DmKeyOf(request), body.Mode), ct));

        app.MapPost("/campaigns/{code}/characters",
            (ISender sender, string code, ImportCharacterRequest request, CancellationToken ct) =>
                sender.Send(new ImportCharacter(code, request.Pathbuilder), ct));

        app.MapPost("/campaigns/{code}/characters/{id:guid}/hit-points",
            async (ISender sender, string code, Guid id, ChangeHitPointsRequest request, CancellationToken ct) =>
                await sender.Send(new ChangeHitPoints(code, id, request.Delta), ct) is { } sheet
                    ? Results.Ok(sheet)
                    : Results.NotFound());

        // PUT rather than POST, because the client names the slot and applying the same effect
        // twice must leave one effect.
        app.MapPut("/campaigns/{code}/characters/{id:guid}/effects/{effectId:guid}",
            async (ISender sender, string code, Guid id, Guid effectId, SetEffectRequest request, CancellationToken ct) =>
                await sender.Send(new SetEffect(code, id, effectId, request.Effect), ct) is { } sheet
                    ? Results.Ok(sheet)
                    : Results.NotFound());

        return app;
    }

    internal static string? DmKeyOf(HttpRequest request) =>
        request.Headers.TryGetValue(DmKeyHeader, out var values) ? values.ToString() : null;
}
