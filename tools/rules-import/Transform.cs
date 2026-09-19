using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pf2e.Tools.RulesImport;

static class Transform
{
    const string SiteRoot = "https://2e.aonprd.com";

    static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Run(IReadOnlyList<string> categories, string? index)
    {
        var dir = ResolveSnapshot(index);
        var manifest = Snapshot.TryReadManifest(dir)
            ?? throw new InvalidOperationException($"snapshot '{dir}' has no manifest.json");

        var outRoot = Path.Combine(Snapshot.FindRepoRoot(), "tools", "rules-import", "out");
        var seedDir = Reset(Path.Combine(outRoot, "seed"));
        var unmappedDir = Reset(Path.Combine(outRoot, "unmapped"));
        var oversizeDir = Reset(Path.Combine(outRoot, "oversize"));
        var excludedDir = Reset(Path.Combine(outRoot, "excluded"));

        Console.WriteLine($"snapshot {manifest.Index} pulled {manifest.PulledAtUtc}");

        var rows = new List<(string Category, int Emitted, int Excluded, int Unmapped, int Oversize)>();
        foreach (var category in categories)
        {
            if (!manifest.Categories.ContainsKey(category))
            {
                throw new InvalidOperationException(
                    $"category '{category}' is not in snapshot '{manifest.Index}', which holds: {string.Join(", ", manifest.Categories.Keys)}");
            }

            var seeds = new List<(string Id, JsonObject Node)>();
            var unmapped = new JsonArray();
            var oversize = new JsonArray();
            var excluded = new JsonArray();

            foreach (var record in ReadCategory(dir, category))
            {
                var id = Str(record, "id");
                var name = Str(record, "name") is { } raw ? Normalise.Text(raw) : null;
                var url = Str(record, "url");

                AssertNoProse(record, id, category);

                if (FieldPolicy.IsSiteExcluded(record))
                {
                    excluded.Add(Excluded(id, name));
                    continue;
                }

                if (id is null or "" || name is null or "" || url is null or "")
                {
                    unmapped.Add(Unmapped(id, name,
                        $"missing or empty field: {string.Join(", ", MissingFields(id, name, url))}"));
                    continue;
                }

                var recordCategory = Str(record, "category");
                if (!string.Equals(recordCategory, category, StringComparison.Ordinal))
                {
                    unmapped.Add(Unmapped(id, name,
                        $"category field '{recordCategory}' does not match file category '{category}'"));
                    continue;
                }

                // Pull already dropped every record superseded by an id it holds, so a remaster_id
                // that survived into the snapshot is by construction dangling. Such a record is the
                // only version we have, so it is seeded and flagged rather than lost.
                var targets = FieldPolicy.LegacyTargets(record);
                if (targets.Count > 0)
                {
                    unmapped.Add(Unmapped(id, name,
                        $"remaster_id points at an id not in the snapshot: {string.Join(", ", targets)}"));
                }

                seeds.Add((id, Project(record, id, name, category, url, oversize)));
            }

            var seedArray = new JsonArray();
            foreach (var seed in seeds.OrderBy(s => s.Id, IdComparer.Instance))
            {
                seedArray.Add(seed.Node);
            }

            Write(Path.Combine(seedDir, category + ".json"), seedArray);
            Write(Path.Combine(unmappedDir, category + ".json"), unmapped);
            Write(Path.Combine(oversizeDir, category + ".json"), oversize);
            Write(Path.Combine(excludedDir, category + ".json"), excluded);
            rows.Add((category, seedArray.Count, excluded.Count, unmapped.Count, oversize.Count));
        }

        PrintSummary(rows);
        WriteAliases(manifest.Index, seedDir);
        return 0;
    }

    /// <summary>
    /// Trims the snapshot's alias file down to the renames that matter and writes it beside the
    /// seed.
    /// <para>Most of what the source calls superseded is a renumbering: the record moved and kept
    /// its name, and 8,650 of the 10,140 whose successor is seeded are exactly that. Those are
    /// dropped, because a direct name match already finds them and an alias that agreed with the
    /// name would only be a second way to get the same answer.</para>
    /// <para>What is left is the genuine renames — Inspire Competence to Uplifting Overture,
    /// Dimension Door to Translocate — which is the only case a Pathbuilder export written before
    /// the Remaster needs.</para>
    /// </summary>
    static void WriteAliases(string index, string seedDir)
    {
        var source = Snapshot.AliasFile(index);
        if (!File.Exists(source))
        {
            Console.WriteLine($"no alias file at {source}; run: rules-import aliases");
            return;
        }

        var seeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(seedDir, "*.json"))
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonArray records)
            {
                continue;
            }

            foreach (var record in records.OfType<JsonObject>())
            {
                var category = Str(record, "category") ?? string.Empty;
                var name = Str(record, "name");
                var id = Str(record, "id");
                if (id is not null)
                {
                    ids.Add(id);
                }

                if (name is not null)
                {
                    if (!seeded.TryGetValue(category, out var names))
                    {
                        names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        seeded[category] = names;
                    }

                    names.Add(name);
                }
            }
        }

        // Keyed case-insensitively because an export's capitalisation is its own, but the name
        // is stored as the source printed it: this file is a record of what things were called.
        var candidates = new Dictionary<(string Category, string Was), (string Printed, HashSet<string> Targets)>();
        foreach (var alias in (JsonNode.Parse(File.ReadAllText(source)) as JsonArray ?? []).OfType<JsonObject>())
        {
            var was = Str(alias, "was");
            var category = Str(alias, "category") ?? string.Empty;
            var nowId = Str(alias, "nowId");

            // Useless unless the successor is actually in the seed, and redundant when the old
            // name still names something.
            if (was is null || nowId is null || !ids.Contains(nowId))
            {
                continue;
            }

            if (seeded.TryGetValue(category, out var names) && names.Contains(was))
            {
                continue;
            }

            var key = (category, was.ToLowerInvariant());
            if (!candidates.TryGetValue(key, out var found))
            {
                found = (was, new HashSet<string>(StringComparer.Ordinal));
                candidates[key] = found;
            }

            found.Targets.Add(nowId);
        }

        var kept = new JsonArray();
        var ambiguous = 0;
        foreach (var ((category, _), (printed, targets)) in candidates.OrderBy(pair => pair.Key.Was, StringComparer.Ordinal))
        {
            // An old name that became several different records is not a rename anybody can
            // follow. "Ability Boosts" was a class feature on twenty-one classes and each of them
            // renamed to its own; answering with one of the twenty-one would be a coin toss
            // dressed up as a lookup.
            if (targets.Count != 1)
            {
                ambiguous++;
                continue;
            }

            kept.Add(new JsonObject
            {
                ["was"] = printed,
                ["category"] = category,
                ["nowId"] = targets.Single(),
            });
        }

        var path = Path.Combine(seedDir, "_aliases.json");
        Write(path, kept);
        Console.WriteLine($"{kept.Count} renames kept, {ambiguous} dropped as ambiguous");
    }

    static JsonObject Project(
        JsonObject record, string id, string name, string category, string url, JsonArray oversize)
    {
        var seed = new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["category"] = category,
            ["sourceUrl"] = SiteRoot + url,
        };

        foreach (var (key, raw) in record.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            if (raw is null || !FieldPolicy.SeedAllowList.Contains(key))
            {
                continue;
            }

            var value = Normalise.Value(key, raw);
            if (FieldPolicy.IsEmptyValue(value))
            {
                continue;
            }

            if (FieldPolicy.CeilingFields.Contains(key))
            {
                var length = FieldPolicy.RenderedLength(value);
                if (length > FieldPolicy.LabelCeiling)
                {
                    oversize.Add(Oversize(id, name, key, length));
                    continue;
                }
            }

            seed[key] = value;
        }

        // Computed rather than copied, so it is not in the allow-list. It is what lets the effect
        // picker apply a record instead of only listing it.
        if (Modifiers.Of(record, category) is { } modifiers)
        {
            seed["modifiers"] = modifiers;
        }

        return seed;
    }

    static void AssertNoProse(JsonObject record, string? id, string category)
    {
        var prose = record.Select(field => field.Key).Where(FieldPolicy.IsProseField).ToList();
        if (prose.Count > 0)
        {
            throw new InvalidOperationException(
                $"record '{id}' of category '{category}' carries prose field(s) {string.Join(", ", prose)}: the snapshot was pulled without the wire excludes and must be re-pulled with --force");
        }
    }

    static string ResolveSnapshot(string? index)
    {
        var root = Snapshot.SnapshotsRoot;
        var candidates = Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).Where(d => Snapshot.TryReadManifest(d) is not null).ToList()
            : [];

        if (index is not null)
        {
            var dir = Snapshot.SnapshotDir(index);
            return candidates.Contains(dir, StringComparer.OrdinalIgnoreCase)
                ? dir
                : throw new InvalidOperationException(
                    $"no snapshot named '{index}' under {root}; candidates: {Names(candidates)}");
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        throw new InvalidOperationException(candidates.Count == 0
            ? $"no snapshot with a readable manifest.json under {root}, run pull first"
            : $"{candidates.Count} snapshots under {root}, pass --index <name>; candidates: {Names(candidates)}");
    }

    static IEnumerable<JsonObject> ReadCategory(string dir, string category)
    {
        var path = Snapshot.CategoryFileIn(dir, category);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"the manifest lists '{category}' but '{path}' is missing");
        }

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, new UTF8Encoding(false));
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            yield return JsonNode.Parse(line) as JsonObject
                ?? throw new InvalidOperationException($"'{path}' holds a line that is not a JSON object");
        }
    }

    static void PrintSummary(IReadOnlyList<(string Category, int Emitted, int Excluded, int Unmapped, int Oversize)> rows)
    {
        Console.WriteLine($"{"category",-16}{"seeded",10}{"excluded",10}{"unmapped",10}{"oversize",10}");
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Category,-16}{row.Emitted,10}{row.Excluded,10}{row.Unmapped,10}{row.Oversize,10}");
        }

        Console.WriteLine(
            $"{"TOTAL",-16}{rows.Sum(r => r.Emitted),10}{rows.Sum(r => r.Excluded),10}{rows.Sum(r => r.Unmapped),10}{rows.Sum(r => r.Oversize),10}");
    }

    // Empties the directory instead of deleting it, so an out/seed that is a link to a shared seed
    // stays a link and the files land where it points.
    static string Reset(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            File.Delete(file);
        }

        return dir;
    }

    static void Write(string path, JsonArray array) =>
        File.WriteAllText(path, array.ToJsonString(OutputOptions), new UTF8Encoding(false));

    static string Names(IEnumerable<string> dirs) => string.Join(", ", dirs.Select(Path.GetFileName));

    static IEnumerable<string> MissingFields(string? id, string? name, string? url)
    {
        if (id is null or "")
        {
            yield return "id";
        }

        if (name is null or "")
        {
            yield return "name";
        }

        if (url is null or "")
        {
            yield return "url";
        }
    }

    static JsonObject Oversize(string id, string name, string field, int length) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["field"] = field,
        ["length"] = length,
    };

    static JsonObject Excluded(string? id, string? name) => new()
    {
        ["id"] = JsonValue.Create(id),
        ["name"] = JsonValue.Create(name),
    };

    static JsonObject Unmapped(string? id, string? name, string reason) => new()
    {
        ["id"] = JsonValue.Create(id),
        ["name"] = JsonValue.Create(name),
        ["reason"] = reason,
    };

    static string? Str(JsonObject record, string field) =>
        record[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
