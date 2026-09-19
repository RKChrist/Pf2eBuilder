using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

/// <summary>
/// Builds the legacy-name index: what a record used to be called, and which record it is now.
/// <para>The pull drops every record superseded by a <c>remaster_id</c>, which is right for the
/// catalogue — a table wants one Raise a Shield, not three — and leaves one thing behind. A
/// Pathbuilder export written before the Remaster carries the old names, so a character's
/// "Inspire Competence" matched nothing and the reference screen could show the name and not open
/// the rule.</para>
/// <para>This fetches names and ids and nothing else. Not a level, not a trait, not a price: the
/// superseded record's own mechanics are not wanted and are not taken. Names are what the
/// Community Use Policy covers and are what this file is.</para>
/// </summary>
static class Aliases
{
    /// <summary>Elasticsearch caps a page at 10,000 and there are fewer superseded records than
    /// that, but paging is how this stays correct if a later index has more.</summary>
    const int PageSize = 1000;

    public static async Task<int> Run(string index, CancellationToken ct = default)
    {
        using var client = new AonClient();

        var found = new List<(string Id, string Name, string Category, List<string> Targets)>();
        object[]? after = null;

        while (true)
        {
            var body = new JsonObject
            {
                ["size"] = PageSize,
                ["_source"] = new JsonArray("id", "name", "category", "remaster_id"),
                // id.keyword, not _id: field data on _id is disabled on this cluster, and the
                // pull already paginates this way for the same reason.
                ["sort"] = new JsonArray(new JsonObject { ["id.keyword"] = "asc" }),
                ["query"] = new JsonObject
                {
                    ["bool"] = new JsonObject
                    {
                        ["must"] = new JsonArray(
                            new JsonObject { ["exists"] = new JsonObject { ["field"] = "remaster_id" } }),
                    },
                },
            };

            if (after is not null)
            {
                body["search_after"] = new JsonArray(JsonValue.Create(after[0]?.ToString()));
            }

            using var page = await client.SearchAsync(body, index, ct);
            var hits = page.RootElement.GetProperty("hits").GetProperty("hits");
            if (hits.GetArrayLength() == 0)
            {
                break;
            }

            string? last = null;
            foreach (var hit in hits.EnumerateArray())
            {
                last = hit.TryGetProperty("sort", out var sort) && sort.GetArrayLength() > 0
                    ? sort[0].GetString()
                    : null;
                var source = hit.GetProperty("_source");

                var targets = Targets(source);
                if (targets.Count == 0 || !source.TryGetProperty("name", out var name)
                    || name.GetString() is not { Length: > 0 } was)
                {
                    continue;
                }

                found.Add((
                    source.TryGetProperty("id", out var ownId) ? ownId.GetString() ?? "" : "",
                    was,
                    source.TryGetProperty("category", out var category) ? category.GetString() ?? "" : "",
                    targets));
            }

            after = last is null ? null : [last];
            if (after is null || hits.GetArrayLength() < PageSize)
            {
                break;
            }
        }

        var written = new JsonArray();
        foreach (var (id, name, category, targets) in found.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            written.Add(new JsonObject
            {
                ["was"] = name,
                ["category"] = category,
                ["nowId"] = targets[0],
            });
        }

        var path = Snapshot.AliasFile(index);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, written.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"{written.Count} superseded names written to {path}");
        return 0;
    }

    /// <summary>AoN writes remaster_id ["0"] on a record with no successor, so "0" is a marker and
    /// not an id. Same rule as <see cref="FieldPolicy.LegacyTargets"/>, over a JsonElement.</summary>
    static List<string> Targets(JsonElement source)
    {
        if (!source.TryGetProperty("remaster_id", out var remaster)
            || remaster.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. remaster.EnumerateArray()
                .Where(node => node.ValueKind is JsonValueKind.String)
                .Select(node => node.GetString()!)
                .Where(text => text.Length > 0 && text != "0"),
        ];
    }
}
