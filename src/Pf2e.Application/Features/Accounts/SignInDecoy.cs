using Pf2e.Application.Abstractions;

namespace Pf2e.Application.Features.Accounts;

/// <summary>
/// A hash for an address nobody registered to be checked against, so that being refused for a
/// wrong password and being refused for an address that does not exist cost the same work.
/// <para>The two already answer in the same words. Without this they would still be told apart
/// by a stopwatch, because one path computes a hash and the other returns as soon as the query
/// comes back empty, and hashing is deliberately slow enough for that difference to be visible
/// across a network. That is the same disclosure arriving by a slower channel.</para>
/// <para>Computed once for the process rather than per request. Per request it would make an
/// unknown address the slower of the two instead of the faster, which is no better. Warmed at
/// startup by Program.cs so that the first refused sign-in is not the one that pays for it.</para>
/// </summary>
public static class SignInDecoy
{
    /// <summary>Not a secret and not a password anybody can present, because nothing reaches
    /// this comparison except a sign-in whose address matched no account at all.</summary>
    const string NobodysPassword = "this-password-belongs-to-no-account";

    static string? _hash;

    public static string For(IPasswordHasher hasher) => _hash ??= hasher.Hash(NobodysPassword);
}
