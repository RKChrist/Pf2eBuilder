using MediatR;
using Pf2e.Application.Features.Tracker;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Api.Endpoints;

public static class TrackerEndpoints
{
    public static IEndpointRouteBuilder MapTracker(this IEndpointRouteBuilder app)
    {
        app.MapGet("/campaigns/{code}", (ISender sender, string code, CancellationToken ct) =>
            sender.Send(new GetCampaign(code), ct));

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
}
