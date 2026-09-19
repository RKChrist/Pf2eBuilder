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

        app.MapPut("/campaigns/{code}/characters/{id:guid}",
            (ISender sender, string code, Guid id, CharacterBuildEdit body, CancellationToken ct) =>
                sender.Send(new EditCharacter(code, id, body), ct));

        // Not DM-only: a player chooses their own activity at a real table.
        app.MapPut("/campaigns/{code}/characters/{id:guid}/exploration",
            (ISender sender, HttpRequest request, string code, Guid id,
             SetExplorationActivityRequest body, CancellationToken ct) =>
                sender.Send(new SetExplorationActivity(code, DmKeyOf(request), id, body.Activity), ct));

        app.MapGet("/exploration-activities", () => Results.Ok(
            Pf2e.Domain.ExplorationActivities.All
                .Select(a => new ExplorationActivityView(a.Key, a.Name, a.Consequence))));

        // One route whether the creature is a character or a monster, because it is one command:
        // the id names a creature and the handler knows which kind it found.
        app.MapPost("/campaigns/{code}/creatures/{id:guid}/hit-points",
            (ISender sender, HttpRequest request, string code, Guid id,
             ChangeHitPointsRequest body, CancellationToken ct) =>
                sender.Send(
                    new ChangeHitPoints(code, DmKeyOf(request), id, body.Amount, Direction(body.Direction)),
                    ct));

        // PUT rather than POST, because the client names the application and applying the same
        // effect twice must leave one effect. It hangs off the campaign rather than off one
        // character, because one application can reach the whole party.
        app.MapPut("/campaigns/{code}/effects/{applicationId:guid}",
            (ISender sender, HttpRequest request, string code, Guid applicationId,
             ApplyEffectRequest body, CancellationToken ct) =>
                sender.Send(
                    new ApplyEffect(code, DmKeyOf(request), applicationId, body.Effect, body.Targets ?? []),
                    ct));

        app.MapPost("/campaigns/{code}/encounter/combatants",
            (ISender sender, HttpRequest request, string code, AddCombatantRequest body, CancellationToken ct) =>
                sender.Send(
                    new AddCombatant(
                        code, DmKeyOf(request), body.RuleId, body.CharacterId, body.Name, body.Initiative),
                    ct));

        app.MapPost("/campaigns/{code}/encounter/initiative",
            (ISender sender, HttpRequest request, string code, RollInitiativeRequest? body, CancellationToken ct) =>
                sender.Send(new RollInitiative(code, DmKeyOf(request), body?.Rolls ?? []), ct));

        app.MapPost("/campaigns/{code}/encounter/undo",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new UndoLastChange(code, DmKeyOf(request)), ct));

        app.MapPost("/campaigns/{code}/encounter/next-turn",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new NextTurn(code, DmKeyOf(request)), ct));

        app.MapPost("/campaigns/{code}/encounter/combatants/{id:guid}/reveal",
            (ISender sender, HttpRequest request, string code, Guid id,
             RevealMonsterRequest body, CancellationToken ct) =>
                sender.Send(new RevealMonster(code, DmKeyOf(request), id, body.Revealed), ct));

        app.MapDelete("/campaigns/{code}/encounter",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new EndEncounter(code, DmKeyOf(request)), ct));

        return app;
    }

    internal static string? DmKeyOf(HttpRequest request) =>
        request.Headers.TryGetValue(DmKeyHeader, out var values) ? values.ToString() : null;

    // Parsed here rather than in the handler, because an unreadable direction is a bad request
    // and the validator is what says so.
    static HitPointDirection Direction(string? named) =>
        Enum.TryParse<HitPointDirection>(named, ignoreCase: true, out var parsed)
            ? parsed
            : throw new BadHttpRequestException("Direction is Damage or Heal.");
}
