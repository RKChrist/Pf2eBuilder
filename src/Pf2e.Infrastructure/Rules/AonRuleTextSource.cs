using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pf2e.Application.Abstractions;

namespace Pf2e.Infrastructure.Rules;

/// <summary>
/// One record's page from the Archives of Nethys index, kept on disk once it has been read.
/// <para>A rule id here is their document id, because the seed was built from the same index. The
/// cache is a file per record and not a table, so that turning this on needs no migration and
/// deleting a folder is the whole of turning it off again.</para>
/// </summary>
public sealed class AonRuleTextSource(
    HttpClient http, IOptions<RuleTextOptions> options, IHostEnvironment host, ILogger<AonRuleTextSource> log)
    : IRuleTextSource
{
    const string NoText = "";

    readonly RuleTextOptions _options = options.Value;

    public async Task<RuleTextAnswer> FetchAsync(string ruleId, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return new RuleTextAnswer.None();
        }

        var file = CacheFile(ruleId);
        if (File.Exists(file))
        {
            return Answer(await File.ReadAllTextAsync(file, ct));
        }

        try
        {
            using var response = await http.GetAsync(
                $"_doc/{Uri.EscapeDataString(ruleId)}?_source=markdown", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return await Remember(file, NoText, ct);
            }

            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var markdown = document.RootElement.TryGetProperty("_source", out var source)
                           && source.TryGetProperty("markdown", out var text)
                           && text.ValueKind is JsonValueKind.String
                ? text.GetString() ?? NoText
                : NoText;
            return await Remember(file, markdown, ct);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or JsonException
                                        && !ct.IsCancellationRequested)
        {
            // Not remembered: the site being down tonight says nothing about tomorrow.
            log.LogWarning(failure, "Could not read the description of {RuleId}", ruleId);
            return new RuleTextAnswer.Unreachable();
        }
    }

    static RuleTextAnswer Answer(string markdown) =>
        markdown.Length == 0 ? new RuleTextAnswer.None() : new RuleTextAnswer.Found(markdown);

    /// <summary>An empty file is an answer too: a record with no page is not asked about twice.</summary>
    static async Task<RuleTextAnswer> Remember(string file, string markdown, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, markdown, ct);
        return Answer(markdown);
    }

    /// <summary>Ids come from the seed and look like <c>spell-1763</c>, but the file name is built
    /// from a filtered copy anyway, because a path is not the place to find out otherwise.</summary>
    string CacheFile(string ruleId)
    {
        var safe = string.Concat(ruleId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        var root = Path.IsPathRooted(_options.CachePath)
            ? _options.CachePath
            : Path.Combine(host.ContentRootPath, _options.CachePath);
        return Path.Combine(root, safe + ".md");
    }
}
