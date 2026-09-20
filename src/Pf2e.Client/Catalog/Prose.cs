using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Pf2e.Client.Catalog;

/// <summary>
/// A rule's description as markup.
/// <para>The server sends a small subset of Markdown and this draws exactly that subset:
/// paragraphs, bold, italics, lists, rules and plain tables. Everything is encoded before any tag
/// of ours goes in, so whatever the text holds, the only markup on screen is what is written
/// below.</para>
/// </summary>
public static partial class Prose
{
    public static MarkupString Render(string markdown)
    {
        var html = new StringBuilder();
        foreach (var block in Blocks().Split(markdown.Replace("\r\n", "\n").Trim()))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                continue;
            }

            if (lines.All(line => Rule().IsMatch(line)))
            {
                html.Append("<hr />");
            }
            else if (lines.All(line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)))
            {
                html.Append("<ul>");
                foreach (var line in lines)
                {
                    html.Append("<li>").Append(Inline(line[2..])).Append("</li>");
                }

                html.Append("</ul>");
            }
            else if (lines.All(line => line.StartsWith('|')))
            {
                Table(html, lines);
            }
            else
            {
                html.Append("<p>").Append(string.Join("<br />", lines.Select(Inline))).Append("</p>");
            }
        }

        return new MarkupString(html.ToString());
    }

    static void Table(StringBuilder html, string[] lines)
    {
        html.Append("<div class=\"prose__table\"><table>");
        var head = true;
        foreach (var line in lines)
        {
            if (Divider().IsMatch(line))
            {
                continue;
            }

            var cell = head ? "th" : "td";
            html.Append("<tr>");
            foreach (var value in line.Trim('|').Split('|'))
            {
                html.Append('<').Append(cell).Append('>').Append(Inline(value.Trim())).Append("</").Append(cell).Append('>');
            }

            html.Append("</tr>");
            head = false;
        }

        html.Append("</table></div>");
    }

    static string Inline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = Bold().Replace(encoded, "<strong>$1</strong>");
        return Italic().Replace(encoded, "<em>$2</em>");
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex Blocks();

    [GeneratedRegex(@"^(-{3,}|\*{3,})$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\|[\s:|-]+\|?$")]
    private static partial Regex Divider();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"(?<![\w*])([*_])(?!\s)(.+?)(?<!\s)\1(?![\w*])")]
    private static partial Regex Italic();
}
