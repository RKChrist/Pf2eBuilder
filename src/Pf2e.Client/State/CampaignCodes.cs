using System.Security.Cryptography;

namespace Pf2e.Client.State;

/// <summary>
/// A campaign code is made up here and the server accepts any valid code, which is why there is no
/// operation that creates one.
/// </summary>
public static class CampaignCodes
{
    public const int Length = 6;

    /// <summary>I, O, 0 and 1 are missing because a code is read out across a table and those
    /// four are the ones people write down as each other.</summary>
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Draw() =>
        string.Create(Length, 0, (span, _) =>
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
