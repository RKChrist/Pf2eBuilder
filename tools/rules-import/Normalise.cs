using System.Collections.Frozen;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Pf2e.Tools.RulesImport;

/// <summary>
/// AoN writes its site's own markup into plain string fields: template markers, HTML, markdown
/// links. The seed carries the words a reader sees and none of the markup, so every consumer
/// downstream can treat a value as text.
/// </summary>
static partial class Normalise
{
    // AoN's action glyphs, spelled the way the seed's own actions field spells them.
    static readonly FrozenDictionary<string, string> Glyphs = new Dictionary<string, string>
    {
        ["oneAction"] = "Single Action",
        ["twoActions"] = "Two Actions",
        ["threeActions"] = "Three Actions",
        ["reaction"] = "Reaction",
        ["freeAction"] = "Free Action",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // A list whose members are a set, so a repeat carries no meaning. Not every list is: an
    // ancestry's attribute list of Free, Free is two boosts.
    static readonly FrozenSet<string> SetFields = new[] { "trait" }.ToFrozenSet(StringComparer.Ordinal);

    public static JsonNode Value(string field, JsonNode node) => node switch
    {
        JsonArray list => List(field, list),
        JsonObject map => new JsonObject(map.Select(entry =>
            KeyValuePair.Create(entry.Key, entry.Value is null ? null : Value(entry.Key, entry.Value)))),
        JsonValue value when value.TryGetValue<string>(out var text) => (JsonNode)Text(text),
        _ => node.DeepClone(),
    };

    public static string Text(string text)
    {
        var plain = Reference().Replace(text, match => match.Groups[1].Value);
        plain = Glyph().Replace(plain, match => Glyphs.TryGetValue(match.Groups[1].Value, out var words)
            ? words
            : throw new InvalidOperationException(
                $"unknown AoN marker '{match.Value}' in \"{text}\"; name its words in Normalise.Glyphs"));
        plain = MarkdownLink().Replace(plain, match => match.Groups[1].Value);
        plain = WebUtility.HtmlDecode(Tag().Replace(plain, string.Empty));
        plain = ClassScoped().Replace(plain, ScopedToClass);
        return Spaces().Replace(plain, " ").Trim();
    }

    static JsonArray List(string field, JsonArray list)
    {
        var members = list.Select(item => item is null ? null : Value(field, item))
                          .Where(item => !FieldPolicy.IsEmptyValue(item));
        if (SetFields.Contains(field))
        {
            members = members.DistinctBy(item => item?.ToJsonString().ToLowerInvariant());
        }

        return new JsonArray([.. members]);
    }

    /// <summary>"[Bard] enigma muse" reads as "enigma muse (Bard)", and "[Fighter] Opening Stance
    /// (Fighter)" already says so and is left alone.</summary>
    static string ScopedToClass(Match match)
    {
        var scope = match.Groups["scope"].Value.Trim();
        var option = match.Groups["option"].Value.Trim();
        return option.EndsWith($"({scope})", StringComparison.OrdinalIgnoreCase) ? option : $"{option} ({scope})";
    }

    [GeneratedRegex("""\{\{\s*\w+\s+\d+\s+"([^"]*)"\s*\}\}""")]
    private static partial Regex Reference();

    [GeneratedRegex("""\{\{\s*(\w+)\s*\}\}""")]
    private static partial Regex Glyph();

    [GeneratedRegex("""_?\[([^\]]+)\]\([^)]*\)_?""")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex("""</?[A-Za-z][^>]*>""")]
    private static partial Regex Tag();

    [GeneratedRegex("""\[(?<scope>[A-Z][^\]]*)\]\s*(?<option>[^;\[]+)""")]
    private static partial Regex ClassScoped();

    [GeneratedRegex("""\s+""")]
    private static partial Regex Spaces();
}
