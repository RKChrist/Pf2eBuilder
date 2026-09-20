using FluentValidation;
using Pf2e.Application.Abstractions;
using Pf2e.Application.Features.Accounts;
using Pf2e.Contracts.Accounts;
using Pf2e.Infrastructure.Security;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// Against the real store and the real hasher, because two of these are about what is on disk
/// and one is about a unique index. A substitute for either would only prove this file's own
/// idea of them.
/// </summary>
public class AccountRules(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    const string Password = "a-long-enough-passphrase";

    IPasswordHasher Hasher { get; } = new PasswordHasher();

    /// <summary>The fixture is one database for the whole class, so every test brings its own
    /// address rather than depending on the order they run in.</summary>
    static string FreshEmail() => $"gnibbo+{Guid.NewGuid():N}@example.test";

    async Task<AccountView> Register(string email, string password = Password, string name = "Gnibbo")
    {
        await using var db = database.NewContext();
        return await new RegisterAccountHandler(db, Hasher)
            .Handle(new RegisterAccount(email, name, password), default);
    }

    async Task<AccountView> SignIn(string email, string password)
    {
        await using var db = database.NewContext();
        return await new SignInHandler(db, Hasher).Handle(new SignIn(email, password), default);
    }

    async Task<string?> StoredHashFor(Guid id)
    {
        await using var db = database.NewContext();
        return (await db.Accounts.FindAsync(id))?.PasswordHash;
    }

    [Fact]
    public async Task RegisteringStoresAHashAndNeverThePasswordItself()
    {
        var account = await Register(FreshEmail());

        var stored = await StoredHashFor(account.Id);

        Assert.NotNull(stored);
        Assert.NotEmpty(stored);
        Assert.DoesNotContain(Password, stored, StringComparison.Ordinal);
        Assert.True(Hasher.Verify(stored, Password), "The stored hash does not verify.");
    }

    [Fact]
    public async Task TwoRegistrationsOfOneAddressAreRefusedRatherThanMakingTwoAccounts()
    {
        var email = FreshEmail();
        await Register(email);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => Register(email));
    }

    /// <summary>
    /// The invariant rather than the path. Two registrations of one address at once may be
    /// settled by the check before the insert or by the unique index after it, depending on how
    /// the two interleave, and both are correct. What must never happen either way is two
    /// accounts on one address, so that is what is asserted.
    /// </summary>
    [Fact]
    public async Task TwoRegistrationsRacingOnOneAddressLeaveExactlyOneAccount()
    {
        var email = FreshEmail();

        var both = await Task.WhenAll(
            Attempt(() => Register(email, name: "First")),
            Attempt(() => Register(email, name: "Second")));

        await using var db = database.NewContext();
        var stored = db.Accounts.Where(a => a.Email == email).ToList();

        Assert.Single(stored);
        Assert.Equal(1, both.Count(outcome => outcome is null));
        Assert.Equal(1, both.Count(outcome => outcome is EmailAlreadyRegisteredException));
    }

    static async Task<Exception?> Attempt(Func<Task<AccountView>> register)
    {
        try
        {
            await register();
            return null;
        }
        catch (Exception failure)
        {
            return failure;
        }
    }

    [Fact]
    public async Task AnAddressRegisteredInOneSpellingIsTheSameAccountInAnother()
    {
        var email = FreshEmail();
        var registered = await Register($"  {email.ToUpperInvariant()} ");

        Assert.Equal(email, registered.Email);

        var signedIn = await SignIn(email, Password);

        Assert.Equal(registered.Id, signedIn.Id);
    }

    [Fact]
    public async Task ASpellingThatDiffersOnlyInCaseIsTheAddressAlreadyTaken()
    {
        var email = FreshEmail();
        await Register(email);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => Register(email.ToUpperInvariant()));
    }

    /// <summary>
    /// The one that matters. Two different sentences here would turn the sign-in form into a way
    /// to ask which addresses have accounts, so this asserts the strings are equal rather than
    /// that both throw.
    /// </summary>
    [Fact]
    public async Task AnUnknownAddressAndAWrongPasswordAreRefusedInTheSameWords()
    {
        var email = FreshEmail();
        await Register(email);

        var wrongPassword = await Assert.ThrowsAsync<SignInRefusedException>(
            () => SignIn(email, "a-different-passphrase"));
        var unknownAddress = await Assert.ThrowsAsync<SignInRefusedException>(
            () => SignIn(FreshEmail(), Password));

        Assert.Equal(wrongPassword.Message, unknownAddress.Message);
        Assert.DoesNotContain(email, unknownAddress.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheRightPasswordAnswersWithTheAccountAndNoHash()
    {
        var email = FreshEmail();
        var registered = await Register(email);

        var signedIn = await SignIn(email, Password);

        Assert.Equal(registered.Id, signedIn.Id);
        Assert.Equal("Gnibbo", signedIn.DisplayName);

        // The stored hash itself, not the name of the property. Asserting the name cannot fail
        // while AccountView has the three fields it has, so it would pass whatever anybody did
        // to it; the value would not.
        var stored = await StoredHashFor(signedIn.Id);
        Assert.DoesNotContain(
            stored!, System.Text.Json.JsonSerializer.Serialize(signedIn), StringComparison.Ordinal);
    }

    /// <summary>Counts the work rather than timing it, because a clock in a test is a flaky
    /// test. Equal verifications is the property that makes equal time true.</summary>
    sealed class CountingHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public int Verifications { get; private set; }

        public string Hash(string password) => inner.Hash(password);

        public bool Verify(string hash, string password)
        {
            Verifications++;
            return inner.Verify(hash, password);
        }
    }

    async Task<int> VerificationsRefusing(string email, string password)
    {
        var counting = new CountingHasher(Hasher);

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<SignInRefusedException>(
            () => new SignInHandler(db, counting).Handle(new SignIn(email, password), default));

        return counting.Verifications;
    }

    /// <summary>
    /// The two refusals already read the same. This is the rest of it: an address nobody
    /// registered has to cost the same work as a wrong password, or the pair is still told apart
    /// by a stopwatch and the shared sentence bought nothing.
    /// </summary>
    [Fact]
    public async Task AnUnknownAddressCostsTheSameHashingAsAWrongPassword()
    {
        var email = FreshEmail();
        await Register(email);

        var wrongPassword = await VerificationsRefusing(email, "a-different-passphrase");
        var unknownAddress = await VerificationsRefusing(FreshEmail(), Password);

        Assert.Equal(1, wrongPassword);
        Assert.Equal(wrongPassword, unknownAddress);
    }

    [Fact]
    public void TheDecoyIsHashedOnceAndReusedRatherThanRecomputedPerRefusal()
    {
        var counting = new CountingHasher(Hasher);

        var first = SignInDecoy.For(counting);
        var second = SignInDecoy.For(counting);

        Assert.Same(first, second);
        Assert.True(Hasher.Verify(first, "this-password-belongs-to-no-account"));
    }

    [Fact]
    public void APasswordShorterThanTheMinimumIsRefusedBeforeItIsEverHashed()
    {
        var result = new RegisterAccountValidator()
            .Validate(new RegisterAccount("gnibbo@example.test", "Gnibbo", "short"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterAccount.Password));
    }

    [Fact]
    public void APasswordLongEnoughToBeAWayToSpendTheProcessorIsRefused()
    {
        var result = new RegisterAccountValidator().Validate(
            new RegisterAccount("gnibbo@example.test", "Gnibbo", new string('p', 100_000)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterAccount.Password));
    }

    [Theory]
    [InlineData("gnibbo")]
    [InlineData("@example.test")]
    [InlineData("gnibbo@")]
    public void SomethingThatIsNotAnAddressIsRefused(string email)
    {
        var result = new RegisterAccountValidator()
            .Validate(new RegisterAccount(email, "Gnibbo", Password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterAccount.Email));
    }

    [Fact]
    public async Task AnAccountThatIsGoneIsReadAsNobodyRatherThanAsAFault()
    {
        await using var db = database.NewContext();

        var account = await new GetAccountHandler(db).Handle(new GetAccount(Guid.NewGuid()), default);

        Assert.Null(account);
    }
}
