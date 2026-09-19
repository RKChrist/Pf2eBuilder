using System.ComponentModel.DataAnnotations;

namespace Pf2e.Api.Infrastructure.Persistence;

public sealed class SeedingOptions
{
    public const string Section = "Seeding";

    public bool Enabled { get; set; } = true;

    /// <summary>Produced by <c>dotnet run --project tools/rules-import -- transform</c>.</summary>
    [Required]
    public string SeedDataPath { get; set; } = string.Empty;

    /// <summary>The snapshot index name. Changing it is what makes the seeder do work again.</summary>
    [Required]
    public string RulesetVersion { get; set; } = string.Empty;
}
