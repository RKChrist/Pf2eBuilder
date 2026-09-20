using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Campaigns;

namespace Pf2e.Infrastructure.Characters;

/// <summary>
/// Their public API, asked about one character by number.
/// <para>Every answer is JSend: a status, and the character under <c>data</c> when there is one.
/// A character its owner has not made public answers as a failure or with no data, and both are
/// the same thing to a player: not shared.</para>
/// </summary>
public sealed class WanderersGuideClient(HttpClient http, ILogger<WanderersGuideClient> log) : IWanderersGuideClient
{
    /// <summary>The character alone, inventory and roll history included, runs to a few hundred
    /// kilobytes. Anything past this is not a character.</summary>
    const int MaxBytes = 8 * 1024 * 1024;

    public async Task<WanderersGuideAnswer> FindCharacterAsync(WanderersGuideCharacterId id, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("find-character", new { id = id.Value }, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new WanderersGuideAnswer.NotShared();
            }

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return new WanderersGuideAnswer.NotFound();
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                return new WanderersGuideAnswer.Unreachable();
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            var succeeded = root.TryGetProperty("status", out var status) && status.GetString() == "success";
            if (!root.TryGetProperty("data", out var data) || data.ValueKind is not JsonValueKind.Object)
            {
                return succeeded ? new WanderersGuideAnswer.NotFound() : new WanderersGuideAnswer.NotShared();
            }

            return new WanderersGuideAnswer.Found(data.GetRawText());
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or JsonException
                                        && !ct.IsCancellationRequested)
        {
            log.LogWarning(failure, "Could not read character {Id} from Wanderer's Guide", id.Value);
            return new WanderersGuideAnswer.Unreachable();
        }
    }
}
