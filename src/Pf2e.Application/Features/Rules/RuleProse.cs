using System.Text.RegularExpressions;

namespace Pf2e.Application.Features.Rules;

/// <summary>
/// The description out of an Archives of Nethys page.
/// <para>A page is a stat header, a rule, and then the words. The header repeats what the seed
/// already holds as mechanics, so only what follows the first rule is kept. Their layout tags come
/// out, and a link keeps its words and loses its address, because the address is a path on their
/// site and the panel has its own way to open a record.</para>
/// </summary>
public static partial class RuleProse
{
    public static string? Of(string markdown)
    {
        var normal = markdown.Replace("\r\n", "\n");
        var rule = HeaderRule().Match(normal);
        var body = rule.Success ? normal[(rule.Index + rule.Length)..] : normal;

        body = Title().Replace(body, match => $"\n\n**{match.Groups[1].Value.Trim()}**\n\n");
        body = ListItem().Replace(body, "\n- ");
        body = Break().Replace(body, "\n");
        body = ListEdge().Replace(body, "\n\n");
        body = Tag().Replace(body, string.Empty);
        body = Link().Replace(body, "$1");
        body = BlankRun().Replace(body, "\n\n").Trim();

        return body.Length == 0 ? null : body;
    }

    [GeneratedRegex(@"^\s*---\s*$\n?", RegexOptions.Multiline)]
    private static partial Regex HeaderRule();

    /// <summary>A heading inside the body, such as a staff's list of spells, written as a title
    /// tag around a link or plain words.</summary>
    [GeneratedRegex(@"<title[^>]*>\s*(.*?)\s*</title>", RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(@"<li[^>]*>")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<br\s*/?>")]
    private static partial Regex Break();

    [GeneratedRegex(@"</?[uo]l[^>]*>")]
    private static partial Regex ListEdge();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"\n[ \t]*(\n[ \t]*)+")]
    private static partial Regex BlankRun();
}
