using System.ComponentModel.DataAnnotations;

namespace Pf2e.Infrastructure.Rules;

public sealed class RuleTextOptions
{
    public const string Section = "RuleText";

    /// <summary>Off means the panel shows mechanics and the link out, as it did before.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The index alias, not a dated index, so a new Archives of Nethys publication does
    /// not need a configuration change here.</summary>
    [Required, Url]
    public string Endpoint { get; set; } = "https://elasticsearch.aonprd.com/aon/";

    /// <summary>Where fetched descriptions are kept, so each record is asked for once. Relative
    /// paths are taken from the content root. Never inside the tracked tree: the repository holds
    /// no prose, and this is what keeps that true.</summary>
    [Required]
    public string CachePath { get; set; } = "App_Data/rule-text";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 8;
}
