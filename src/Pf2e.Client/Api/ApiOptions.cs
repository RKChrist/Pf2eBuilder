namespace Pf2e.Client.Api;

public sealed class ApiOptions
{
    public const string Section = "Api";

    public string BaseUrl { get; set; } = string.Empty;

    public int SearchDebounceMilliseconds { get; set; }
}
