using System.Security.Cryptography;
using System.Text;

namespace Pf2e.Domain;

/// <summary>
/// Who is asking. There is no authentication yet and this is not an identity: it is the
/// distinction the projection needs, and phase 3 replaces how it is established without
/// changing anything downstream of it.
/// </summary>
public enum ViewerRole
{
    Player,
    Dm,
}

/// <summary>
/// The second secret. The player code is what everyone types and reads out; the DM key is
/// longer, is kept by whoever created the campaign, and is the only thing that promotes a
/// connection to <see cref="ViewerRole.Dm"/>.
/// </summary>
public static class DmKey
{
    /// <summary>192 bits, base64url. Long enough that guessing it is not a strategy, short
    /// enough to paste.</summary>
    public const int EntropyBytes = 24;

    public static string Draw() =>
        Base64Url(RandomNumberGenerator.GetBytes(EntropyBytes));

    /// <summary>
    /// The one place a role is decided. Compared in fixed time because the key is a bearer
    /// secret and an ordinary string comparison tells an attacker how much of a guess was right.
    /// </summary>
    public static ViewerRole RoleFor(string storedKey, string? presented)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(storedKey))
        {
            return ViewerRole.Player;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedKey), Encoding.UTF8.GetBytes(presented))
            ? ViewerRole.Dm
            : ViewerRole.Player;
    }

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
