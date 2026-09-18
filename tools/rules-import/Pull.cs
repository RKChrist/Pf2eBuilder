using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

static class Pull
{
    const int PageSize = 500;
    const string SortSpec = "id.keyword:asc";
    static readonly TimeSpan PageDelay = TimeSpan.FromMilliseconds(250);

    public static async Task<int> RunAsync(IReadOnlyList<string> categories, bool force)
    {
        if (!force && FindSatisfyingSnapshot(categories) is { } present)
        {
            Console.WriteLine($"snapshot already present, nothing to pull: {present}");
            return 0;
        }

        using var client = new AonClient();
        var (index, indexed, ids) = await StageAsync(client, categories);
        Console.WriteLine($"staged {ids.Count} ids from {indexed.Count} categories of index {index}");

        var stats = Filter(index, indexed, ids);
        foreach (var (category, stat) in stats)
        {
            Console.WriteLine(
                $"{category,-14} indexed {stat.Indexed,6}  written {stat.Written,6}  legacy {stat.DroppedAsLegacy,6}  danglingRemaster {stat.LegacyTargetMissing,5}");
        }

        var manifest = new Manifest(
            index,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            AonClient.Endpoint,
            PageSize,
            SortSpec,
            FieldPolicy.WireExcludes,
            FieldPolicy.ProseFieldOrder,
            FieldPolicy.Categories.All(stats.ContainsKey),
            stats);

        WriteAtomic(Snapshot.ManifestFile(index), JsonSerializer.Serialize(manifest, Snapshot.ManifestOptions));

        foreach (var category in stats.Keys)
        {
            File.Delete(Snapshot.StagingFile(index, category));
        }

        Console.WriteLine($"wrote {Snapshot.SnapshotDir(index)}");
        return 0;
    }

    static async Task<(string Index, OrderedDictionary<string, int> Indexed, HashSet<string> Ids)> StageAsync(
        AonClient client, IReadOnlyList<string> categories)
    {
        string? index = null;
        var indexed = new OrderedDictionary<string, int>();

        // One id set spanning every requested category, built before anything is dropped, so a
        // chained supersession A -> B -> C still recognises B as superseded and drops A and B both.
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in categories)
        {
            using var firstDoc = await client.SearchAsync(Body(category, null));
            var hits = firstDoc.RootElement.GetProperty("hits");
            var total = hits.GetProperty("total").GetProperty("value").GetInt32();
            var firstPage = hits.GetProperty("hits");
            index ??= LearnIndex(firstPage, category);
            Directory.CreateDirectory(Snapshot.SnapshotDir(index));

            using (var writer = OpenStaging(Snapshot.StagingFile(index, category)))
            {
                var count = StagePage(writer, firstPage, index, ids);
                var cursor = count == PageSize ? LastSort(firstPage) : null;

                while (cursor is not null)
                {
                    await Task.Delay(PageDelay);
                    using var doc = await client.SearchAsync(Body(category, cursor));
                    var next = doc.RootElement.GetProperty("hits").GetProperty("hits");
                    count = StagePage(writer, next, index, ids);
                    cursor = count == PageSize ? LastSort(next) : null;
                }
            }

            indexed.Add(category, total);
        }

        return index is null
            ? throw new InvalidOperationException("no categories were requested, so no snapshot was written")
            : (index, indexed, ids);
    }

    static OrderedDictionary<string, CategoryStat> Filter(
        string index, OrderedDictionary<string, int> indexed, HashSet<string> ids)
    {
        var stats = new OrderedDictionary<string, CategoryStat>();

        foreach (var (category, total) in indexed)
        {
            var staging = Snapshot.StagingFile(index, category);
            var final = Snapshot.CategoryFile(index, category);
            var temp = final + ".tmp";
            var written = 0;
            var dropped = 0;
            var targetMissing = 0;

            // A crash must never leave a half-written file sitting under the final name.
            using (var reader = new StreamReader(staging, new UTF8Encoding(false)))
            using (var writer = OpenNdjson(temp))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var record = JsonNode.Parse(line) as JsonObject
                        ?? throw new InvalidOperationException($"'{staging}' holds a line that is not a JSON object");

                    var targets = FieldPolicy.LegacyTargets(record);
                    if (targets.Any(ids.Contains))
                    {
                        dropped++;
                        continue;
                    }

                    if (targets.Count > 0)
                    {
                        targetMissing++;
                    }

                    writer.WriteLine(line);
                    written++;
                }
            }

            if (written + dropped != total)
            {
                throw new InvalidOperationException(
                    $"category '{category}' wrote {written} and dropped {dropped} records, which is {written + dropped}, but the index reported {total}");
            }

            File.Move(temp, final, overwrite: true);
            stats.Add(category, new CategoryStat(total, written, dropped, targetMissing));
        }

        return stats;
    }

    static string? FindSatisfyingSnapshot(IReadOnlyList<string> categories)
    {
        var root = Snapshot.SnapshotsRoot;
        if (!Directory.Exists(root))
        {
            return null;
        }

        // complete:true is deliberately not part of this test: a --only pull legitimately leaves it false.
        return Directory.EnumerateDirectories(root).FirstOrDefault(dir =>
            Snapshot.TryReadManifest(dir) is { } manifest &&
            categories.All(c => manifest.Categories.ContainsKey(c) && File.Exists(Snapshot.CategoryFileIn(dir, c))));
    }

    // search_after, not from/size: from+size caps at 10,000 and equipment alone holds ~9,100 records,
    // so from/size would silently truncate as soon as the index grows.
    static JsonObject Body(string category, JsonArray? searchAfter)
    {
        var excludes = new JsonArray();
        foreach (var pattern in FieldPolicy.WireExcludes)
        {
            excludes.Add(pattern);
        }

        var body = new JsonObject
        {
            ["size"] = PageSize,
            ["track_total_hits"] = true,
            ["sort"] = new JsonArray(new JsonObject { ["id.keyword"] = "asc" }),
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["filter"] = new JsonArray(new JsonObject
                    {
                        ["term"] = new JsonObject { ["category"] = category },
                    }),
                },
            },
            ["_source"] = new JsonObject { ["excludes"] = excludes },
        };

        if (searchAfter is not null)
        {
            body["search_after"] = searchAfter;
        }

        return body;
    }

    static string LearnIndex(JsonElement page, string category)
    {
        if (page.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"category '{category}' returned no hits, so the index name cannot be learned");
        }

        return page[0].GetProperty("_index").GetString()
            ?? throw new InvalidOperationException($"category '{category}' returned a hit with no _index");
    }

    static int StagePage(TextWriter writer, JsonElement page, string index, HashSet<string> ids)
    {
        var count = 0;

        foreach (var hit in page.EnumerateArray())
        {
            var hitIndex = hit.GetProperty("_index").GetString();
            if (!string.Equals(hitIndex, index, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"a hit came from index '{hitIndex}' but the pull started on '{index}': the site re-indexed mid-pull and the snapshot would be inconsistent");
            }

            var source = hit.GetProperty("_source");
            writer.WriteLine(JsonSerializer.Serialize(source));
            if (source.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                id.GetString() is { Length: > 0 } text)
            {
                ids.Add(text);
            }

            count++;
        }

        return count;
    }

    static JsonArray LastSort(JsonElement page)
    {
        var last = page[page.GetArrayLength() - 1];
        return JsonSerializer.Deserialize<JsonArray>(last.GetProperty("sort"))
            ?? throw new InvalidOperationException("the last hit of a full page carried no sort value");
    }

    static StreamWriter OpenStaging(string path) =>
        new(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None), new UTF8Encoding(false))
        {
            NewLine = "\n",
        };

    static StreamWriter OpenNdjson(string path)
    {
        var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var gzip = new GZipStream(file, CompressionLevel.Optimal);
        return new StreamWriter(gzip, new UTF8Encoding(false)) { NewLine = "\n" };
    }

    static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
