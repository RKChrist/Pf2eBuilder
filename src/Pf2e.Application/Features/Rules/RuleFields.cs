using System.Text.Json.Nodes;

namespace Pf2e.Application.Features.Rules;

/// <summary>One value out of a record's flattened mechanics, for the handful of places that want
/// a single field and not the whole document.</summary>
internal static class RuleFields
{
    public static string? Text(string mechanics, string key)
    {
        // A key absent from the text cannot be present in the document, and this runs once per
        // row of every action in the ruleset.
        if (!mechanics.Contains($"\"{key}\"", StringComparison.Ordinal))
        {
            return null;
        }

        return JsonNode.Parse(mechanics) is JsonObject fields
               && fields[key] is JsonValue value
               && value.TryGetValue<string>(out var text)
               && text.Length > 0
            ? text
            : null;
    }
}
