using MediatR;
using Pf2e.Application.Features.Campaigns;
using Pf2e.Application.Features.Rules;
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

        // These two are the mutating routes that never read the DM key, and it is deliberate.
        // Bringing your character in and correcting it is your own business at a real table, and
        // the campaign code is the credential that says you are at that table: a code buys a
        // seat, the DM key buys the fight. Gating the edit alone would secure nothing either,
        // since anyone who can import can already replace a character wholesale by name.
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

        app.MapPost("/campaigns/{code}/characters/{id:guid}/camp",
            (ISender sender, HttpRequest request, string code, Guid id,
             CampActivityRequest body, CancellationToken ct) =>
                sender.Send(new TakeCampActivity(code, DmKeyOf(request), id, body.Activity), ct));

        // A camping session. Everything hangs off /camp, because it is one value on the campaign
        // and these are the ways it changes.
        app.MapPut("/campaigns/{code}/camp/zone",
            (ISender sender, HttpRequest request, string code, SetCampZoneRequest body, CancellationToken ct) =>
                sender.Send(new SetCampZone(code, DmKeyOf(request), body.ZoneName, body.ZoneDc, body.EncounterDc), ct));

        app.MapPut("/campaigns/{code}/camp/campsite",
            (ISender sender, HttpRequest request, string code, RecordCampsiteRequest body, CancellationToken ct) =>
                sender.Send(new RecordCampsite(code, DmKeyOf(request), body.Outcome), ct));

        app.MapPost("/campaigns/{code}/camp/characters/{id:guid}/activities",
            (ISender sender, HttpRequest request, string code, Guid id,
             TakeCampingActivityRequest body, CancellationToken ct) =>
                sender.Send(new TakeCampingActivity(code, DmKeyOf(request), id, body.Activity, body.Outcome), ct));

        app.MapPut("/campaigns/{code}/camp/characters/{id:guid}/meal",
            (ISender sender, HttpRequest request, string code, Guid id, ChooseMealRequest body, CancellationToken ct) =>
                sender.Send(new ChooseMeal(code, DmKeyOf(request), id, body.Kind, body.RecipeId, body.RuleId), ct));

        app.MapPut("/campaigns/{code}/camp/book/{id:guid}",
            (ISender sender, HttpRequest request, string code, Guid id, SaveCampEntryRequest body, CancellationToken ct) =>
                sender.Send(new SaveCampEntry(code, DmKeyOf(request), id, body.Kind, body.Name, body.Does, body.Dc), ct));

        app.MapDelete("/campaigns/{code}/camp/book/{id:guid}",
            (ISender sender, HttpRequest request, string code, Guid id, CancellationToken ct) =>
                sender.Send(new RemoveCampEntry(code, DmKeyOf(request), id), ct));

        app.MapPut("/campaigns/{code}/camp/supplies",
            (ISender sender, HttpRequest request, string code, SetCampSuppliesRequest body, CancellationToken ct) =>
                sender.Send(new SetCampSupplies(code, DmKeyOf(request), body.BasicIngredients, body.SpecialIngredients), ct));

        app.MapPost("/campaigns/{code}/camp/break",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new BreakCamp(code, DmKeyOf(request)), ct));

        app.MapPost("/campaigns/{code}/time",
            (ISender sender, HttpRequest request, string code, PassTimeRequest body, CancellationToken ct) =>
                sender.Send(new PassTime(code, DmKeyOf(request), body.Minutes), ct));

        app.MapPost("/campaigns/{code}/rest",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new RestForTheNight(code, DmKeyOf(request)), ct));

        app.MapPut("/campaigns/{code}/characters/{id:guid}/downtime",
            (ISender sender, HttpRequest request, string code, Guid id,
             SetDowntimeActivityRequest body, CancellationToken ct) =>
                sender.Send(
                    new SetDowntimeActivity(code, DmKeyOf(request), id, body.Activity, body.TaskLevel),
                    ct));

        app.MapPost("/campaigns/{code}/day",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new AdvanceDay(code, DmKeyOf(request)), ct));

        app.MapGet("/downtime-activities", () => Results.Ok(
            Pf2e.Domain.DowntimeActivities.All
                .Select(a => new DowntimeActivityView(a.Key, a.Name, a.Skill, a.What))));

        // From the ruleset rather than from a table in this codebase: the camping activities are
        // seeded actions carrying the Camping trait, requirements and all.
        app.MapGet("/camping-activities", (ISender sender, CancellationToken ct) =>
            sender.Send(new GetCampingActivities(), ct));

        app.MapGet("/campsite-meals", (ISender sender, CancellationToken ct) =>
            sender.Send(new GetCampsiteMeals(), ct));

        app.MapGet("/camp-activities", () => Results.Ok(
            Pf2e.Domain.CampActivities.All
                .Select(a => new CampActivityView(a.Key, a.Name, a.Minutes, a.What))));

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
                        code, DmKeyOf(request), body.RuleId, body.CharacterId, body.Name, body.Initiative,
                        body.Homebrew, body.Count ?? 1),
                    ct));

        // DELETE, because what goes is the DM's own version of the monster. The monster stays.
        app.MapDelete("/campaigns/{code}/encounter/combatants/{id:guid}/monster",
            (ISender sender, HttpRequest request, string code, Guid id, CancellationToken ct) =>
                sender.Send(new ResetMonster(code, DmKeyOf(request), id), ct));

        app.MapPut("/campaigns/{code}/encounter/combatants/{id:guid}/monster",
            (ISender sender, HttpRequest request, string code, Guid id,
             EditMonsterRequest body, CancellationToken ct) =>
                sender.Send(new EditMonster(code, DmKeyOf(request), id, body), ct));

        app.MapPost("/campaigns/{code}/encounter/initiative",
            (ISender sender, HttpRequest request, string code, RollInitiativeRequest? body, CancellationToken ct) =>
                sender.Send(new RollInitiative(code, DmKeyOf(request), body?.Rolls ?? []), ct));

        app.MapPost("/campaigns/{code}/encounter/undo",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new UndoLastChange(code, DmKeyOf(request)), ct));

        app.MapPost("/campaigns/{code}/encounter/next-turn",
            (ISender sender, HttpRequest request, string code, CancellationToken ct) =>
                sender.Send(new NextTurn(code, DmKeyOf(request)), ct));

        app.MapPut("/campaigns/{code}/encounter/combatants/{id:guid}/initiative",
            (ISender sender, HttpRequest request, string code, Guid id,
             SetInitiativeRequest body, CancellationToken ct) =>
                sender.Send(new SetInitiative(code, DmKeyOf(request), id, body.Initiative), ct));

        app.MapDelete("/campaigns/{code}/encounter/combatants/{id:guid}",
            (ISender sender, HttpRequest request, string code, Guid id, CancellationToken ct) =>
                sender.Send(new RemoveCombatant(code, DmKeyOf(request), id), ct));

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
