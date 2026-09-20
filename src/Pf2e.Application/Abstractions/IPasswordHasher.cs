namespace Pf2e.Application.Abstractions;

/// <summary>
/// Which algorithm, at what cost, is an infrastructure decision with a package behind it, and
/// this layer is not allowed to know about either.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>The stored hash comes first because it is the input that decides the work: it
    /// carries the salt and the iteration count the password has to be put through to be
    /// compared at all.</summary>
    bool Verify(string hash, string password);
}
