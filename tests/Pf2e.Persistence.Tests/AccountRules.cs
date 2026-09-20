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
        Assert.DoesNotContain(
            nameof(Domain.Accounts.Account.PasswordHash),
            System.Text.Json.JsonSerializer.Serialize(signedIn),
            StringComparison.Ordinal);
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
