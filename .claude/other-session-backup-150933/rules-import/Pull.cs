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
        var pulled = new Dictionary<string, CategoryStat>(StringComparer.Ordinal);
        string? index = null;

        foreach (var category in categories)
        {
            var (stat, learned) = await PullCategoryAsync(client, category, index);
            index = learned;
            pulled[category] = stat;
            Console.WriteLine(
                $"{category,-14} records {stat.Records,6}  legacy {stat.Legacy,6}  expected {stat.ExpectedFromIndex,6}");
            if (stat.Records != stat.ExpectedFromIndex)
            {
                Console.WriteLine(
                    $"warning: {category} wrote {stat.Records} records but the index reported {stat.ExpectedFromIndex}");
            }
        }

        if (index is null)
        {
            throw new InvalidOperationException("no categories were requested, so no snapshot was written");
        }

        var stats = new OrderedDictionary<string, CategoryStat>();
        foreach (var category in FieldPolicy.Categories.Where(pulled.ContainsKey))
        {
            stats.Add(category, pulled[category]);
        }

        var manifest = new Manifest(
            index,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            AonClient.Endpoint,
            PageSize,
            SortSpec,
            FieldPolicy.ProseFieldOrder,
            FieldPolicy.Categories.All(pulled.ContainsKey),
            stats);

        WriteAtomic(Snapshot.ManifestFile(index), JsonSerializer.Serialize(manifest, Snapshot.ManifestOptions));
        Console.WriteLine($"wrote {Snapshot.SnapshotDir(index)}");
        return 0;
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

    static async Task<(CategoryStat Stat, string Index)> PullCategoryAsync(
        AonClient client, string category, string? knownIndex)
    {
        using var firstDoc = await client.SearchAsync(Body(category, null));
        var hits = firstDoc.RootElement.GetProperty("hits");
        var expected = hits.GetProperty("total").GetProperty("value").GetInt32();
        var firstPage = hits.GetProperty("hits");
        var index = knownIndex ?? LearnIndex(firstPage, category);

        Directory.CreateDirectory(Snapshot.SnapshotDir(index));
        var final = Snapshot.CategoryFile(index, category);
        var temp = final + ".tmp";

        var records = 0;
        var legacy = 0;

        // A crash must never leave a half-written file sitting under the final name.
        using (var writer = OpenNdjson(temp))
        {
            var page = WritePage(writer, firstPage, index);
            records += page.Count;
            legacy += page.Legacy;
            var cursor = page.Count == PageSize ? LastSort(firstPage) : null;

            while (cursor is not null)
            {
                await Task.Delay(PageDelay);
                using var doc = await client.SearchAsync(Body(category, cursor));
                var next = doc.RootElement.GetProperty("hits").GetProperty("hits");
                page = WritePage(writer, next, index);
                records += page.Count;
                legacy += page.Legacy;
                cursor = page.Count == PageSize ? LastSort(next) : null;
            }
        }

        File.Move(temp, final, overwrite: true);
        return (new CategoryStat(expected, records, legacy), index);
    }

    // search_after, not from/size: from+size caps at 10,000 and equipment alone holds ~9,100 records,
    // so from/size would silently truncate as soon as the index grows.
    static JsonObject Body(string category, JsonArray? searchAfter)
    {
        var excludes = new JsonArray();
        foreach (var field in FieldPolicy.ProseFieldOrder)
        {
            excludes.Add(field);
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

    static (int Count, int Legacy) WritePage(TextWriter writer, JsonElement page, string index)
    {
        var count = 0;
        var legacy = 0;

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
            if (source.TryGetProperty("remaster_id", out var remaster) &&
                remaster.ValueKind == JsonValueKind.Array && remaster.GetArrayLength() > 0)
            {
                legacy++;
            }

            count++;
        }

        return (count, legacy);
    }

    static JsonArray LastSort(JsonElement page)
    {
        var last = page[page.GetArrayLength() - 1];
        return JsonSerializer.Deserialize<JsonArray>(last.GetProperty("sort"))
            ?? throw new InvalidOperationException("the last hit of a full page carried no sort value");
    }

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
