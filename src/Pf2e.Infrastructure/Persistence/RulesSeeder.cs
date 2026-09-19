using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pf2e.Domain.Rules;

namespace Pf2e.Infrastructure.Persistence;

public sealed record SeedingReport(
    bool Skipped,
    int RecordCount,
    IReadOnlyDictionary<string, int> PerCategory,
    TimeSpan Elapsed);

public sealed class RulesSeeder(RulesDbContext db, IOptions<SeedingOptions> options, ILogger<RulesSeeder> log)
{
    // Promoted to real columns, so they are removed from the JSON blob rather than stored twice.
    static readonly string[] Promoted =
        ["id", "name", "category", "sourceUrl", "level", "rarity", "type", "primary_source", "trait"];

    readonly SeedingOptions _options = options.Value;

    public async Task<SeedingReport> SeedAsync(CancellationToken ct = default)
    {
        var started = Stopwatch.StartNew();
        var directory = Path.GetFullPath(_options.SeedDataPath);

        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").OrderBy(f => f).ToArray()
            : [];

        // An empty rules database is a worse outcome than a refusal to start, because the
        // application looks healthy and every character it builds is silently wrong.
        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"Seeding is enabled but no seed files were found in '{directory}'. " +
                "Produce them with: dotnet run --project tools/rules-import -- transform");
        }

        var records = new List<RuleRecord>();
        var perCategory = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var category = Path.GetFileNameWithoutExtension(file);
            await using var stream = File.OpenRead(file);
            var array = (await JsonNode.ParseAsync(stream, cancellationToken: ct))!.AsArray();

            foreach (var node in array)
            {
                records.Add(ToRecord(node!.AsObject()));
            }

            perCategory[category] = array.Count;
        }

        var existing = await db.SeedState.SingleOrDefaultAsync(ct);
        if (existing is not null
            && existing.RulesetVersion == _options.RulesetVersion
            && existing.RecordCount == records.Count)
        {
            log.LogInformation("Rules data already seeded at {Version}, {Count} records.",
                existing.RulesetVersion, existing.RecordCount);
            return new SeedingReport(true, existing.RecordCount, perCategory, started.Elapsed);
        }

        // Replace rather than merge, so a forced reseed converges on the same end state
        // whatever the database held before.
        await db.RuleRecords.ExecuteDeleteAsync(ct);
        db.RuleRecords.AddRange(records);

        if (existing is null)
        {
            db.SeedState.Add(new SeedState
            {
                RulesetVersion = _options.RulesetVersion,
                RecordCount = records.Count,
                SeededAtUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.RulesetVersion = _options.RulesetVersion;
            existing.RecordCount = records.Count;
            existing.SeededAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        log.LogInformation("Seeded {Count} records across {Categories} categories in {Elapsed}.",
            records.Count, perCategory.Count, started.Elapsed);

        return new SeedingReport(false, records.Count, perCategory, started.Elapsed);
    }

    RuleRecord ToRecord(JsonObject source)
    {
        var mechanics = new JsonObject();
        foreach (var (key, value) in source)
        {
            if (!Promoted.Contains(key, StringComparer.Ordinal))
            {
                mechanics[key] = value?.DeepClone();
            }
        }

        return new RuleRecord
        {
            Id = source["id"]!.GetValue<string>(),
            Name = source["name"]!.GetValue<string>(),
            Category = source["category"]!.GetValue<string>(),
            SourceUrl = source["sourceUrl"]!.GetValue<string>(),
            RulesetVersion = _options.RulesetVersion,
            Level = source["level"] is { } level ? level.GetValue<int>() : null,
            Rarity = source["rarity"]?.GetValue<string>(),
            Type = source["type"]?.GetValue<string>(),
            PrimarySource = source["primary_source"]?.GetValue<string>(),
            Traits = StringList(source["trait"]),
            Mechanics = mechanics.ToJsonString(),
        };
    }

    // AoN is inconsistent about whether a single-valued list is a list, so accept both.
    static List<string> StringList(JsonNode? node) => node switch
    {
        JsonArray array => [.. array.Select(item => item!.GetValue<string>())],
        JsonValue value => [value.GetValue<string>()],
        _ => [],
    };
}
