using System.Text.Json.Nodes;
using Pf2e.Contracts.Rules;
using Pf2e.Domain.Rules;

namespace Pf2e.Application.Features.Rules;

internal static class RuleSummaries
{
    public static RuleSummary Of(RuleRecord r) => Of(r, RuleMechanics.Fields(r.Mechanics));

    public static RuleSummary Of(RuleRecord r, IReadOnlyList<MechanicField> mechanics) =>
        new(r.Id, r.Category, r.Name, r.Level, r.Rarity, r.PrimarySource, r.Traits, r.SourceUrl,
            Highlights.Of(r.Category, mechanics));
}

internal static class RuleMechanics
{
    /// <summary>Key order is the seed's own, which is the only ordering a field this server has
    /// never heard of can be given.</summary>
    public static IReadOnlyList<MechanicField> Fields(string json) =>
        JsonNode.Parse(json) is JsonObject fields
            ? [.. fields.Select(field => new MechanicField(field.Key, Flatten(field.Value)))]
            : [];

    static IReadOnlyList<string> Flatten(JsonNode? node) => node switch
    {
        JsonArray array => [.. array.Select(Render)],
        null => [],
        _ => [Render(node)],
    };

    static string Render(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue(out string? text) => text,
        _ => node.ToJsonString(),
    };
}
