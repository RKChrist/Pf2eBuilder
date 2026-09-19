using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Client.Api;

public sealed class TrackerApiException(string message) : Exception(message);

public sealed class TrackerApi(HttpClient http)
{
    public Task<TableView> GetTableAsync(string code, CancellationToken ct) =>
        SendAsync<TableView>(new HttpRequestMessage(HttpMethod.Get, Table(code)), ct);

    public Task<CharacterSheetView> ImportAsync(string code, string pathbuilder, CancellationToken ct) =>
        SendAsync<CharacterSheetView>(
            Carrying(HttpMethod.Post, $"{Table(code)}/characters", new ImportCharacterRequest(pathbuilder)),
            ct);

    public Task<CharacterSheetView> ChangeHitPointsAsync(
        string code, Guid character, int delta, CancellationToken ct) =>
        SendAsync<CharacterSheetView>(
            Carrying(
                HttpMethod.Post,
                $"{Table(code)}/characters/{character}/hit-points",
                new ChangeHitPointsRequest(delta)),
            ct);

    public Task<CharacterSheetView> SetEffectAsync(
        string code, Guid character, Guid slot, EffectSpec? effect, CancellationToken ct) =>
        SendAsync<CharacterSheetView>(
            Carrying(
                HttpMethod.Put,
                $"{Table(code)}/characters/{character}/effects/{slot}",
                new SetEffectRequest(effect)),
            ct);

    static string Table(string code) => $"tables/{Uri.EscapeDataString(code)}";

    static HttpRequestMessage Carrying<T>(HttpMethod method, string url, T body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new TrackerApiException("The table service is not reachable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new TrackerApiException(await ReadFailureAsync(response, ct));
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                   ?? throw new TrackerApiException("The table service sent an empty answer.");
        }
        catch (Exception error) when (error is not (TrackerApiException or OperationCanceledException))
        {
            throw new TrackerApiException("The table service sent something this app could not read.");
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
            return "That character is no longer at this table.";
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

        return "The table service could not do that.";
    }
}
