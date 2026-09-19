using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

sealed class AonClient : IDisposable
{
    public const string Endpoint = "https://elasticsearch.aonprd.com/aon/_search";
    public const string UserAgent =
        "Pf2eBuilder-rules-import/1.0 (Pathfinder 2e character builder; non-commercial; sequential requests)";

    const int RequestCap = 300;
    const int BodyPreviewChars = 500;

    static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16),
    ];

    static readonly int[] RetryableStatuses = [429, 502, 503, 504];

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    int _requests;

    public AonClient() => _http.DefaultRequestHeaders.Add("User-Agent", UserAgent);

    /// <summary>
    /// <paramref name="index"/> names the index to search instead of the "aon" alias. The alias
    /// follows whatever Archives of Nethys published last, and a pull that mixed one index's ids
    /// with another's names would produce an alias file pointing at records the snapshot does
    /// not hold. Naming the index is how the two stay the same vintage.
    /// </summary>
    public async Task<JsonDocument> SearchAsync(
        JsonObject body, string? index = null, CancellationToken ct = default)
    {
        var url = index is { Length: > 0 }
            ? $"https://elasticsearch.aonprd.com/{Uri.EscapeDataString(index)}/_search"
            : Endpoint;
        var payload = body.ToJsonString();

        for (var attempt = 0; ; attempt++)
        {
            if (_requests >= RequestCap)
            {
                throw new InvalidOperationException(
                    $"the request cap of {RequestCap} tripped, refusing to send another request to {url}");
            }

            _requests++;
            HttpResponseMessage response;
            try
            {
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                response = await _http.PostAsync(url, content, ct);
            }
            catch (Exception ex) when (attempt < Backoff.Length && IsTransient(ex, ct))
            {
                await Task.Delay(Backoff[attempt], ct);
                continue;
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                }

                if (RetryableStatuses.Contains(status) && attempt < Backoff.Length)
                {
                    await Task.Delay(RetryAfter(response) ?? Backoff[attempt], ct);
                    continue;
                }

                var text = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"search failed with HTTP {status}: {Preview(text)}");
            }
        }
    }

    public void Dispose() => _http.Dispose();

    static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var delta = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return delta is null ? null : delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
    }

    static string Preview(string text) =>
        text.Length <= BodyPreviewChars ? text : text[..BodyPreviewChars] + "...";
}
