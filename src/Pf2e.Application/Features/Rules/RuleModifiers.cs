using System.Text.Json.Nodes;
using Pf2e.Contracts.Tracker;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// The one reader of a rule record's extracted modifiers, so the list and the detail cannot
/// disagree about what a record states. Every record answers empty until the ingest work that
/// fills the key lands; the read path exists so that work is a data change and not a migration.
/// </summary>
internal static class RuleModifiers
{
    const string Key = "\"modifiers\"";

    public static bool Present(string mechanics) => Of(mechanics).Count > 0;

    public static IReadOnlyList<EffectModifierView> Of(string mechanics)
    {
        // A key absent from the text cannot be present in the document, and this runs once per
        // row of every search page.
        if (!mechanics.Contains(Key, StringComparison.Ordinal))
        {
            return [];
        }

        return JsonNode.Parse(mechanics) is JsonObject fields && fields["modifiers"] is JsonArray modifiers
            ? [.. modifiers.Select(Modifier).OfType<EffectModifierView>()]
            : [];
    }

    static EffectModifierView? Modifier(JsonNode? node) => node is JsonObject modifier
        ? new EffectModifierView(
            Text(modifier["type"]) ?? "Untyped",
            Number(modifier["value"]),
            [.. Selectors(modifier["applies"])])
        : null;

    static IEnumerable<SelectorSpecView> Selectors(JsonNode? node) => node is JsonArray applies
        ? applies.Select(Selector).OfType<SelectorSpecView>()
        : [];

    static SelectorSpecView? Selector(JsonNode? node) => node is JsonObject selector
        ? new SelectorSpecView(
            Text(selector["kind"]) ?? string.Empty,
            Text(selector["stat"]),
            Text(selector["attribute"]),
            Text(selector["skillName"]))
        : null;

    static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    static int Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int number) ? number : 0;
}
