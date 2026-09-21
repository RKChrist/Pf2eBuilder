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

    /// <summary>
    /// Whether running this project in Development also runs the client, so that pressing run on
    /// one project runs the app. Ignored outside Development, where a web process starting
    /// another process is nobody's idea of a deployment.
    /// </summary>
    public bool StartClient { get; set; } = true;

    /// <summary>The client project, relative to this one's content root.</summary>
    public string ClientProject { get; set; } = "../Pf2e.Client";

    /// <summary>Where the client will be once it is up. Only used to see whether somebody is
    /// already there, so that a client running in another window is left alone.</summary>
    public string ClientUrl { get; set; } = "http://localhost:5173";
}
