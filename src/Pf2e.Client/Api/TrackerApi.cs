using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Api;

public sealed class CampaignApiException(string message) : Exception(message);

public sealed class TrackerApi(HttpClient http)
{
    /// <summary>The DM key this browser holds, which is only ever the one that came back from
    /// creating a campaign. It rides on every request from then on and is what the server reads
    /// to decide the role; nothing in this class decides anything about it.</summary>
    public const string DmKeyHeader = "X-DM-Key";

    string? _dmKey;

    public void UseDmKey(string key) => _dmKey = key;

    public Task<CreatedCampaignView> CreateCampaignAsync(CancellationToken ct) =>
        SendAsync<CreatedCampaignView>(new HttpRequestMessage(HttpMethod.Post, "campaigns"), ct);

    public Task<CampaignView> GetCampaignAsync(string code, CancellationToken ct) =>
        SendAsync<CampaignView>(new HttpRequestMessage(HttpMethod.Get, Campaign(code)), ct);

    public Task<CampaignModeView> SetModeAsync(string code, string mode, CancellationToken ct) =>
        SendAsync<CampaignModeView>(
            Carrying(HttpMethod.Post, $"{Campaign(code)}/mode", new SetModeRequest(mode)), ct);

    public Task<CharacterSheetView> ImportAsync(string code, string pathbuilder, CancellationToken ct) =>
        SendAsync<CharacterSheetView>(
            Carrying(HttpMethod.Post, $"{Campaign(code)}/characters", new ImportCharacterRequest(pathbuilder)),
            ct);

    /// <summary>An amount and a direction, so a hundred points of damage is one request rather
    /// than a hundred taps.</summary>
    public Task<CampaignView> ChangeHitPointsAsync(
        string code, Guid creature, int amount, string direction, CancellationToken ct) =>
        SendAsync<CampaignView>(
            Carrying(
                HttpMethod.Post,
                $"{Campaign(code)}/creatures/{creature}/hit-points",
                new ChangeHitPointsRequest(amount, direction)),
            ct);

    public Task<CampaignView> ApplyEffectAsync(
        string code, Guid application, EffectSpec? effect, IReadOnlyList<EffectTargetSpec> targets,
        CancellationToken ct) =>
        SendAsync<CampaignView>(
            Carrying(
                HttpMethod.Put,
                $"{Campaign(code)}/effects/{application}",
                new ApplyEffectRequest(effect, targets)),
            ct);

    /// <summary>The build layer only. A re-import replaces the same fields, so an edit and a
    /// re-import are the same operation with a different source.</summary>
    public Task<CharacterSheetView> EditCharacterAsync(
        string code, Guid character, CharacterBuildEdit edit, CancellationToken ct) =>
        SendAsync<CharacterSheetView>(
            Carrying(HttpMethod.Put, $"{Campaign(code)}/characters/{character}", edit), ct);

    /// <summary>A monster names a creature record; a player names somebody already on the
    /// roster. One route, because it is one command either way.</summary>
    public Task<CampaignView> AddCombatantAsync(
        string code, string? ruleId, Guid? characterId, string? name, int? initiative, CancellationToken ct) =>
        SendAsync<CampaignView>(
            Carrying(
                HttpMethod.Post,
                $"{Campaign(code)}/encounter/combatants",
                new AddCombatantRequest(ruleId, characterId, name, initiative)),
            ct);

    /// <summary>Combatants the rolls do not name have their initiative rolled by the server, so
    /// an empty list rolls the whole encounter.</summary>
    public Task<CampaignView> RollInitiativeAsync(
        string code, IReadOnlyList<InitiativeRoll> rolls, CancellationToken ct) =>
        SendAsync<CampaignView>(
            Carrying(HttpMethod.Post, $"{Campaign(code)}/encounter/initiative", new RollInitiativeRequest(rolls)),
            ct);

    public Task<CampaignView> NextTurnAsync(string code, CancellationToken ct) =>
        SendAsync<CampaignView>(new HttpRequestMessage(HttpMethod.Post, $"{Campaign(code)}/encounter/next-turn"), ct);

    public Task<CampaignView> UndoAsync(string code, CancellationToken ct) =>
        SendAsync<CampaignView>(new HttpRequestMessage(HttpMethod.Post, $"{Campaign(code)}/encounter/undo"), ct);

    public Task<CampaignView> RevealAsync(string code, Guid combatant, bool revealed, CancellationToken ct) =>
        SendAsync<CampaignView>(
            Carrying(
                HttpMethod.Post,
                $"{Campaign(code)}/encounter/combatants/{combatant}/reveal",
                new RevealMonsterRequest(revealed)),
            ct);

    public Task<CampaignView> EndEncounterAsync(string code, CancellationToken ct) =>
        SendAsync<CampaignView>(new HttpRequestMessage(HttpMethod.Delete, $"{Campaign(code)}/encounter"), ct);

    /// <summary>Whether this browser holds a DM key at all, which is what the shell reads to
    /// decide between offering the DM controls and not drawing them.</summary>
    public bool HoldsDmKey => _dmKey is { Length: > 0 };

    static string Campaign(string code) => $"campaigns/{Uri.EscapeDataString(code)}";

    static HttpRequestMessage Carrying<T>(HttpMethod method, string url, T body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        if (_dmKey is { Length: > 0 } key)
        {
            request.Headers.Add(DmKeyHeader, key);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new CampaignApiException("The campaign service is not reachable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CampaignApiException(await ReadFailureAsync(response, ct));
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                   ?? throw new CampaignApiException("The campaign service sent an empty answer.");
        }
        catch (Exception error) when (error is not (CampaignApiException or OperationCanceledException))
        {
            throw new CampaignApiException("The campaign service sent something this app could not read.");
        }
    }

    /// <summary>
    /// A refusal is the player's to read, so the server's own sentence wins where it sent one.
    /// A paste that is not an export and a code that is not a code both arrive this way, and a
    /// status code would tell the player nothing they can act on.
    /// </summary>
    static async Task<string> ReadFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return "That character is no longer in this campaign.";
        }

        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (body.ValueKind is JsonValueKind.Object
                && body.TryGetProperty("title", out var title)
                && title.GetString() is { Length: > 0 } sentence)
            {
                return sentence;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
        }

        return "The campaign service could not do that.";
    }
}
