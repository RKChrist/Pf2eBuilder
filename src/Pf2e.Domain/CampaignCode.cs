using System.Security.Cryptography;

namespace Pf2e.Domain;

/// <summary>
/// A campaign is a short code and anyone with the code is in that campaign. The code is drawn
/// by whoever creates the campaign, which is the server, because a campaign now comes into
/// being through an operation rather than by being typed at.
/// </summary>
public static class CampaignCode
{
    public const int GeneratedLength = 6;

    /// <summary>I, O, 0 and 1 are missing because a code is read out across a table and those
    /// four are the ones people write down as each other.</summary>
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Draw() =>
        string.Create(GeneratedLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsValid(string code)
    {
        var normalized = Normalize(code);
        return normalized.Length is >= 4 and <= 12
            && normalized.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
    }
}
