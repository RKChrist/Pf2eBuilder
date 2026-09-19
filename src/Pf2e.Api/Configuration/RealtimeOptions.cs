using System.ComponentModel.DataAnnotations;

namespace Pf2e.Api.Configuration;

public sealed class RealtimeOptions
{
    public const string Section = "Realtime";

    [Required]
    public string HubPath { get; set; } = "/hub/table";

    /// <summary>A table runs on phone wifi, where an idle connection is dropped by something in
    /// the middle long before either end notices.</summary>
    [Range(1, 300)]
    public int KeepAliveSeconds { get; set; } = 15;

    [Range(1, 600)]
    public int ClientTimeoutSeconds { get; set; } = 30;
}
