namespace Pf2e.Domain.Accounts;

/// <summary>
/// Somebody who can be recognised again on a different device. An account sits underneath the
/// campaign code and the DM key and replaces neither: a code still buys a seat and the DM key
/// still buys the fight, whether or not the browser holding them has a name.
/// </summary>
public sealed class Account
{
    public required Guid Id { get; init; }

    /// <summary>The identifier, stored trimmed and lower-cased. Two spellings of one address are
    /// one account, which is why it is the stored form that carries a unique index and not the
    /// form somebody typed.</summary>
    public required string Email { get; set; }

    /// <summary>What a person is called at the table, which is nobody's business but theirs and
    /// is never what they are looked up by.</summary>
    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
