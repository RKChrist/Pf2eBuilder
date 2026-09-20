using Pf2e.Contracts.Accounts;
using Pf2e.Domain.Accounts;

namespace Pf2e.Application.Features.Accounts;

/// <summary>
/// The one place an address is turned into the identifier it is stored and looked up under, and
/// the one place the rules on the two fields every account operation carries are written.
/// Normalising in a validator would leave the handler holding whatever the caller typed, so the
/// handlers call <see cref="Normalise"/> and the unique index sees one string per person.
/// </summary>
public static class AccountCredentials
{
    /// <summary>The longest address a mail server is obliged to accept.</summary>
    public const int MaxEmail = 254;

    public const int MaxDisplayName = 128;

    public const int MinPassword = 12;

    /// <summary>Not a rule about passwords. Hashing is deliberately expensive, so an unbounded
    /// string is a way to spend this server's processor from a phone.</summary>
    public const int MaxPassword = 256;

    public static string Normalise(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>An @ with something on either side, and nothing more. Anything stricter refuses
    /// addresses that work, and nothing this side of sending mail can tell whether one exists.
    /// </summary>
    public static bool IsEmail(string? email)
    {
        var address = Normalise(email);
        var at = address.IndexOf('@', StringComparison.Ordinal);

        return at > 0 && at < address.Length - 1;
    }

    public static bool FitsInAnAddressField(string? email) => Normalise(email).Length <= MaxEmail;

    public static AccountView ViewOf(Account account) =>
        new(account.Id, account.Email, account.DisplayName);
}
