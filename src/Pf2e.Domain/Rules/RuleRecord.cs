namespace Pf2e.Domain.Rules;

/// <summary>
/// One record of the seeded ruleset. The fields a query filters on are properties; the other
/// ninety-odd, which are sparse and differ per category, stay as a JSON document in
/// <see cref="Mechanics"/>. This type knows nothing about how it is stored.
/// </summary>
public sealed class RuleRecord
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required string SourceUrl { get; init; }
    public required string RulesetVersion { get; init; }
    public int? Level { get; init; }
    public string? Rarity { get; init; }
    public string? Type { get; init; }
    public string? PrimarySource { get; init; }
    public List<string> Traits { get; init; } = [];
    public required string Mechanics { get; init; }
}
