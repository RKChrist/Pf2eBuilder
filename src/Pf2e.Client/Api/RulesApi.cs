using System.Net;
using System.Net.Http.Json;
using Pf2e.Contracts.Rules;

namespace Pf2e.Client.Api;

public sealed class RulesApiException(string message) : Exception(message);

public sealed class RulesApi(HttpClient http)
{
    public Task<RuleSearchResult> SearchAsync(
        string? category, string query, int? minLevel, int? maxLevel, string? trait, int page, CancellationToken ct,
        int? pageSize = null)
    {
        var url = new UrlBuilder("rules")
            .Add("Category", category)
            .Add("Name", query)
            .Add("MinLevel", minLevel?.ToString())
            .Add("MaxLevel", maxLevel?.ToString())
            .Add("Trait", trait)
            .Add("Page", page.ToString())
            .Add("PageSize", pageSize?.ToString())
            .ToString();

        return GetAsync<RuleSearchResult>(url, ct);
    }

    public Task<RuleCounts> CountAsync(string? name, string? trait, CancellationToken ct) =>
        GetAsync<RuleCounts>(new UrlBuilder("rules/counts").Add("Name", name).Add("Trait", trait).ToString(), ct);

    /// <summary>The record in <paramref name="category"/>, or in any category when that is null, named exactly <paramref name="name"/>,
    /// which a ranked search puts first when there is one.</summary>
    public async Task<RuleSummary?> FindAsync(string? category, string name, CancellationToken ct)
    {
        var found = await SearchAsync(category, name, null, null, null, 1, ct, pageSize: 5);
        return found.Items.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A record's description. Null when the ruleset has no record by that id.</summary>
    public async Task<RuleText?> GetRuleTextAsync(string id, CancellationToken ct) =>
        await GetAsync<RuleText>(new UrlBuilder($"rules/{Uri.EscapeDataString(id)}/text").ToString(), ct, missingIsNull: true);

    public Task<TraitCounts> CountTraitsAsync(string category, string? name, int? minLevel, int? maxLevel, CancellationToken ct) =>
        GetAsync<TraitCounts>(new UrlBuilder("rules/traits")
            .Add("Category", category)
            .Add("Name", name)
            .Add("MinLevel", minLevel?.ToString())
            .Add("MaxLevel", maxLevel?.ToString())
            .ToString(), ct);

    /// <summary>Null when the ruleset has no record by that id, which is an answer and not a failure:
    /// trying again will not change it.</summary>
    public async Task<RuleDetail?> GetRuleAsync(string id, CancellationToken ct) =>
        await GetAsync<RuleDetail>(new UrlBuilder($"rules/{Uri.EscapeDataString(id)}").ToString(), ct, missingIsNull: true);

    public Task<IReadOnlyList<ConditionSummary>> GetConditionsAsync(CancellationToken ct) =>
        GetAsync<IReadOnlyList<ConditionSummary>>("conditions", ct);

    async Task<T> GetAsync<T>(string url, CancellationToken ct) where T : class =>
        await GetAsync<T>(url, ct, missingIsNull: false)
        ?? throw new RulesApiException("The rules service sent an empty answer.");

    async Task<T?> GetAsync<T>(string url, CancellationToken ct, bool missingIsNull) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, ct);
        }
        catch (HttpRequestException)
        {
            throw new RulesApiException("The rules service is not reachable.");
        }

        if (response.StatusCode is HttpStatusCode.NotFound && missingIsNull)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new RulesApiException($"The rules service returned {(int)response.StatusCode}.");
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                   ?? throw new RulesApiException("The rules service sent an empty answer.");
        }
        catch (Exception error) when (error is not (RulesApiException or OperationCanceledException))
        {
            throw new RulesApiException("The rules service sent something this app could not read.");
        }
    }

    sealed class UrlBuilder(string path)
    {
        readonly List<string> _parameters = [];

        public UrlBuilder Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _parameters.Add($"{name}={Uri.EscapeDataString(value)}");
            }

            return this;
        }

        public override string ToString() =>
            _parameters.Count == 0 ? path : $"{path}?{string.Join('&', _parameters)}";
    }
}
