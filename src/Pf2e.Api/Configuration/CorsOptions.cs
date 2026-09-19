using System.ComponentModel.DataAnnotations;

namespace Pf2e.Api.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";

    /// <summary>
    /// The client is a separate deployable on its own origin, so the browser will not call this
    /// API until these are named. Listed by the operator rather than defaulted to a wildcard,
    /// because a wildcard that works in development is the reason one ships to production.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];
}
