namespace Pf2e.Client.Api;

public sealed class ApiOptions
{
    public const string Section = "Api";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>How long the name filter waits for the player to stop typing.</summary>
    public int SearchDebounceMilliseconds { get; set; }
}
