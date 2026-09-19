using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

static class FieldPolicy
{
    public static readonly IReadOnlyList<string> Categories =
    [
        "equipment", "feat", "action", "spell", "class-feature", "trait",
        "deity", "background", "weapon", "heritage", "archetype", "ritual",
        "language", "condition", "ancestry", "armor", "class", "skill",
    ];

    // We are licensed to store names and mechanics, not prose.
    public static readonly IReadOnlyList<string> ProseFieldOrder =
    [
        "markdown", "text", "search_markdown", "summary", "summary_markdown",
        "source_markdown", "requirement", "requirement_markdown", "access",
        "trigger", "trigger_markdown", "edict", "anathema",
    ];

    public static readonly IReadOnlySet<string> ProseFields = ProseFieldOrder.ToHashSet(StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> DroppedFields = new[]
    {
        "exclude_from_search", "navigation", "image", "icon_image", "religious_symbol", "url",
    }.ToHashSet(StringComparer.Ordinal);

    public static bool IsDroppedField(string name) =>
        name.EndsWith("_markdown", StringComparison.Ordinal) || DroppedFields.Contains(name);

    public static bool IsEmptyValue(JsonNode? value) => value switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        JsonValue v => v.TryGetValue<string>(out var s) && s.Length == 0,
        _ => false,
    };
}
