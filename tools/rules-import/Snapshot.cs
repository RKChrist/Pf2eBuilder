using System.Text.Json;

namespace Pf2e.Tools.RulesImport;

record CategoryStat(int Indexed, int Written, int DroppedAsLegacy, int LegacyTargetMissing);

record Manifest(
    string Index,
    string PulledAtUtc,
    string Endpoint,
    int PageSize,
    string Sort,
    IReadOnlyList<string> ExcludedFieldPatterns,
    IReadOnlyList<string> ExcludedFields,
    bool Complete,
    OrderedDictionary<string, CategoryStat> Categories);

static class Snapshot
{
    public const string SolutionMarker = "Pf2eBuilder.sln";

    public static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionMarker)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"no ancestor of '{AppContext.BaseDirectory}' contains {SolutionMarker}, so the repository root cannot be located");
    }

    public static string SnapshotsRoot => Path.Combine(FindRepoRoot(), "Sources", "aon-snapshot");

    public static string SnapshotDir(string index) => Path.Combine(SnapshotsRoot, index);

    public static string CategoryFile(string index, string category) =>
        CategoryFileIn(SnapshotDir(index), category);

    public static string CategoryFileIn(string dir, string category) =>
        Path.Combine(dir, category + ".ndjson.gz");

    public static string StagingFile(string index, string category) =>
        Path.Combine(SnapshotDir(index), category + ".staging.ndjson");

    public static string ManifestFile(string index) => Path.Combine(SnapshotDir(index), "manifest.json");

    public static Manifest? TryReadManifest(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), ManifestOptions);
        if (manifest is null || manifest.Categories is null)
        {
            throw new InvalidOperationException($"manifest '{path}' is malformed and has no categories");
        }

        return manifest;
    }
}

sealed class IdComparer : IComparer<string>
{
    public static readonly IdComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var left = (x ?? string.Empty).Split('-');
        var right = (y ?? string.Empty).Split('-');
        var shared = Math.Min(left.Length, right.Length);

        for (var i = 0; i < shared; i++)
        {
            var order = long.TryParse(left[i], out var a) && long.TryParse(right[i], out var b)
                ? a.CompareTo(b)
                : StringComparer.Ordinal.Compare(left[i], right[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
