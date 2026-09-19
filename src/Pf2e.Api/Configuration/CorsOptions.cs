using System.ComponentModel.DataAnnotations;

namespace Pf2e.Api.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";

    /// <summary>Required rather than defaulted, because a wildcard that works in development is
    /// how one reaches production.</summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];
}
