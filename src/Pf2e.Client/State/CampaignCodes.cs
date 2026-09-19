namespace Pf2e.Client.State;

/// <summary>
/// What this screen accepts in the join field, so a mistyped code is refused before a round
/// trip. Drawing a code is not here: a campaign is created by an operation on the server, which
/// is what makes the DM key reach exactly one person.
/// </summary>
public static class CampaignCodes
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsValid(string code)
    {
        var normalized = Normalize(code);
        return normalized.Length is >= 4 and <= 12
               && normalized.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
    }
}
