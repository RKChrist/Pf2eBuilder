using Microsoft.AspNetCore.Identity;
using Pf2e.Application.Abstractions;
using Pf2e.Domain.Accounts;
using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Pf2e.Domain.Accounts.Account>;

namespace Pf2e.Infrastructure.Security;

/// <summary>
/// ASP.NET Identity's hasher without ASP.NET Identity. Only Microsoft.Extensions.Identity.Core
/// is referenced, which is the abstractions and this one class rather than the user store, the
/// sign-in manager and the schema that come with the full package. The hash it returns carries
/// its own algorithm, salt and iteration count, so raising the work later does not invalidate
/// what is already stored.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    readonly IdentityHasher _hasher = new();

    /// <summary>
    /// The type parameter exists so an application can vary the work factor per user, which this
    /// one does not, and the hasher never reads the instance. One throwaway is handed over
    /// rather than null, so a null check added to the library later cannot turn every sign-in
    /// into an exception.
    /// </summary>
    static readonly Account Unread = new()
    {
        Id = Guid.Empty,
        Email = string.Empty,
        DisplayName = string.Empty,
        PasswordHash = string.Empty,
        CreatedUtc = default,
    };

    public string Hash(string password) => _hasher.HashPassword(Unread, password);

    // SuccessRehashNeeded is a success that also says the stored hash was made with a weaker
    // setting than the current one. Nothing re-hashes yet, so it is treated as the success it is
    // rather than as a refusal of a correct password.
    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(Unread, hash, password) is not PasswordVerificationResult.Failed;
}
