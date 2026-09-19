using System.Globalization;
using Microsoft.AspNetCore.Components;
using Pf2e.Client.Catalog;
using Pf2e.Client.State;

namespace Pf2e.Client.Components;

/// <summary>
/// Every address the app builds. The board and a category's list are pages with addresses of
/// their own, so refresh and back keep the reader's place. A record and a trait open as sheets
/// over whatever screen is showing, owned by the query string so the phone's back button closes
/// them. Opening a record drops any trait sheet, because the record is what the reader asked for
/// next.
/// </summary>
public static class Links
{
    public const string SearchRoute = "/search";

    public const string BrowsePrefix = "/browse/";

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

    /// <summary>The first group is the app's front door, so its board is plain /.</summary>
    public static string Board(GroupKey group) =>
        group == RuleGroups.All[0].Key ? "/" : $"/?group={group.ToString().ToLowerInvariant()}";

    public static GroupKey? GroupNamed(string? name) =>
        Enum.TryParse<GroupKey>(name, ignoreCase: true, out var group) && Enum.IsDefined(group) ? group : null;

    /// <summary>A category with a screen of its own, such as conditions, opens that instead.</summary>
    public static string Category(RuleCategory category) => category.OwnRoute ?? Category(new CategoryAddress(category.Key));

    /// <summary>
    /// q, min, max, with and page. The trait filter is "with" because "trait" already names the
    /// trait sheet, which opens over a list without changing it.
    /// </summary>
    public static string Category(CategoryAddress address)
    {
        var parameters = Parameters(address)
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value!)}")
            .ToList();

        var path = BrowsePrefix + Uri.EscapeDataString(address.Category);
        return parameters.Count == 0 ? path : $"{path}?{string.Join('&', parameters)}";
    }

    /// <summary>The same list at another address, keeping any record or trait sheet open over it:
    /// a filter changes what is listed, not what is being read.</summary>
    public static string Category(this NavigationManager navigation, CategoryAddress address) =>
        navigation.GetUriWithQueryParameters(Parameters(address).ToDictionary(pair => pair.Key, pair => (object?)pair.Value));

    static IReadOnlyList<KeyValuePair<string, string?>> Parameters(CategoryAddress address) =>
    [
        new("q", address.Query.Trim().Length > 0 ? address.Query : null),
        new("min", address.MinLevel?.ToString(CultureInfo.InvariantCulture)),
        new("max", address.MaxLevel?.ToString(CultureInfo.InvariantCulture)),
        new("with", address.Trait),
        new("page", address.Page > 1 ? address.Page.ToString(CultureInfo.InvariantCulture) : null),
    ];

    public static string Rule(this NavigationManager navigation, string id) =>
        navigation.GetUriWithQueryParameters(new Dictionary<string, object?> { ["rule"] = id, ["trait"] = null });

    public static string Trait(this NavigationManager navigation, string name) =>
        navigation.GetUriWithQueryParameter("trait", name);

    public static bool IsOn(this NavigationManager navigation, string route) =>
        new Uri(navigation.Uri).AbsolutePath.Equals(route, StringComparison.OrdinalIgnoreCase);
}
