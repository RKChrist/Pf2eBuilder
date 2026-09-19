using System.Text.Json.Nodes;
using Pf2e.Contracts.Rules;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

internal static class RuleSummaries
{
    public static RuleSummary Of(RuleRecord r) => Of(r, RuleMechanics.Fields(r.Mechanics));

    public static RuleSummary Of(RuleRecord r, IReadOnlyList<MechanicField> mechanics) =>
        new(r.Id, r.Category, r.Name, r.Level, r.Rarity, r.PrimarySource, r.Traits, r.SourceUrl,
            Highlights.Of(r.Category, mechanics),
            RuleModifiers.Present(r.Mechanics));
}

internal static class RuleMechanics
{
    /// <summary>Key order is the seed's own, which is the only ordering a field this server has
    /// never heard of can be given.</summary>
    public static IReadOnlyList<MechanicField> Fields(string json) =>
        JsonNode.Parse(json) is JsonObject fields
            ? [.. fields.Select(field => new MechanicField(field.Key, Flatten(field.Key, field.Value)))
                        .Where(field => field.Values.Count > 0)]
            : [];

    static IReadOnlyList<string> Flatten(string key, JsonNode? node) => node switch
    {
        JsonArray array => [.. array.Select(Render)],
        JsonObject map => Readings.TryGetValue(key, out var read) ? read(map) : Pairs(map),
        null => [],
        _ => [Render(node)],
    };

    /// <summary>A structured field read the way the rules print it. Keyed by field, so a field with
    /// no row here still reads as name and value pairs rather than as JSON.</summary>
    static readonly Dictionary<string, Func<JsonObject, IReadOnlyList<string>>> Readings = new(StringComparer.Ordinal)
    {
        ["speed"] = Speeds,
    };

    /// <summary>Land speed first and unnamed, then every other movement by name. AoN's "max" is the
    /// fastest of the others, a sort key and not a speed.</summary>
    static IReadOnlyList<string> Speeds(JsonObject speeds) =>
    [
        .. speeds.Where(speed => speed.Key != "max" && speed.Value is not null)
            .OrderBy(speed => speed.Key == "land" ? 0 : 1)
            .ThenBy(speed => speed.Key, StringComparer.Ordinal)
            .Select(speed => speed.Key == "land" ? $"{Render(speed.Value)} feet" : $"{speed.Key} {Render(speed.Value)} feet"),
    ];

    static IReadOnlyList<string> Pairs(JsonObject map) =>
    [
        .. map.Where(pair => pair.Value is not null).Select(pair => $"{pair.Key.Replace('_', ' ')} {Render(pair.Value)}"),
    ];

    static string Render(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue(out string? text) => text,
        _ => node.ToJsonString(),
    };
}
