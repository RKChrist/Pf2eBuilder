using Microsoft.AspNetCore.Components;

namespace Pf2e.Client.Components;

/// <summary>
/// Every address the app builds. A record and a trait open as sheets over whatever screen is
/// showing, owned by the query string so the phone's back button closes them. Opening a record
/// drops any trait sheet, because the record is what the reader asked for next.
/// </summary>
public static class Links
{
    public const string SearchRoute = "/search";

    public static string Search(string query, string? scope = null, int page = 1)
    {
        var parameters = new List<string> { $"q={Uri.EscapeDataString(query.Trim())}" };
        if (scope is not null)
        {
            parameters.Add($"in={Uri.EscapeDataString(scope)}");
        }

        if (page > 1)
        {
            parameters.Add($"page={page}");
        }

        return $"{SearchRoute}?{string.Join('&', parameters)}";
    }

    public static string Rule(this NavigationManager navigation, string id) =>
        navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { ["rule"] = id, ["trait"] = null });

    public static string Trait(this NavigationManager navigation, string name) =>
        navigation.GetUriWithQueryParameter("trait", name);

    public static bool IsOn(this NavigationManager navigation, string route) =>
        new Uri(navigation.Uri).AbsolutePath.Equals(route, StringComparison.OrdinalIgnoreCase);
}
