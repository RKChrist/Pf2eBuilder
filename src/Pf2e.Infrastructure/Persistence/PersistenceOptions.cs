using System.ComponentModel.DataAnnotations;

namespace Pf2e.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string Section = "Database";

    /// <summary>Selected here rather than named in code, so the engine is an operator decision.</summary>
    [Required]
    public string Provider { get; set; } = "Sqlite";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool MigrateOnStartup { get; set; } = true;
}
