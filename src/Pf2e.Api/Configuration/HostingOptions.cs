namespace Pf2e.Api.Configuration;

public sealed class HostingOptions
{
    public const string Section = "Hosting";

    /// <summary>
    /// A published client to serve from this process. Empty means serve nothing, which is the
    /// two-process development run. Set it and the API becomes the only origin, which is what
    /// one ngrok tunnel needs and what removes CORS from the picture entirely.
    /// </summary>
    public string ClientRoot { get; set; } = string.Empty;
}
