using System.Globalization;
using System.Text.RegularExpressions;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Which character a Wanderer's Guide link points at.
/// <para>The number is all that ever leaves here. The server asks one fixed host about an
/// integer and never fetches the address somebody pasted, so a link is a way to name a
/// character and not a way to make this server call somewhere.</para>
/// </summary>
public readonly partial record struct WanderersGuideCharacterId(int Value)
{
    /// <summary>A link to a stat block, a sheet or the builder, or the bare number. Anything on
    /// another host is not a link to a character, whatever its path says.</summary>
    public static bool TryParse(string? text, out WanderersGuideCharacterId id)
    {
        id = default;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 256)
        {
            return false;
        }

        if (Digits().IsMatch(trimmed))
        {
            return Number(trimmed, out id);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var link)
            || (link.Scheme != Uri.UriSchemeHttps && link.Scheme != Uri.UriSchemeHttp)
            || link.UserInfo.Length > 0
            || !(link.Host.Equals("wanderersguide.app", StringComparison.OrdinalIgnoreCase)
                 || link.Host.Equals("www.wanderersguide.app", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var path = CharacterPath().Match(link.AbsolutePath);
        return path.Success && Number(path.Groups[1].Value, out id);
    }

    static bool Number(string digits, out WanderersGuideCharacterId id)
    {
        id = default;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return false;
        }

        id = new WanderersGuideCharacterId(value);
        return true;
    }

    [GeneratedRegex(@"^\d{1,9}$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^/(?:stat-block/character|sheet|builder)/(\d{1,9})(?:/|$)")]
    private static partial Regex CharacterPath();
}
