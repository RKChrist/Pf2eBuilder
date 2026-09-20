using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Application.Features.Accounts;

/// <summary>
/// The one refusal a sign-in ever gives. It carries no argument on purpose: an address nobody
/// registered and a password that is wrong have to be indistinguishable, or the form becomes a
/// way to ask which addresses exist.
/// </summary>
public sealed class SignInRefusedException() : Exception(
    "That email address and password do not match an account.");

public sealed record SignIn(string Email, string Password) : IRequest<AccountView>;

public sealed class SignInValidator : AbstractValidator<SignIn>
{
    public SignInValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Sign in with the address you registered.");

        // The maximum is here because it is the denial-of-service guard and it has to hold on
        // the route anybody can call. The minimum is not: it is a rule about what you may
        // choose, and enforcing it here would answer a too-short password with a different
        // sentence from a wrong one, which is the disclosure this whole file is shaped around.
        RuleFor(c => c.Password)
            .Must(password => password is not null && password.Length <= AccountCredentials.MaxPassword)
            .WithMessage($"A password is at most {AccountCredentials.MaxPassword} characters.");
    }
}

public sealed class SignInHandler(IAccountsDbContext db, IPasswordHasher hasher)
    : IRequestHandler<SignIn, AccountView>
{
    /// <summary>
    /// One refusal from two causes, thrown from one place so the two can never drift apart, and
    /// reached by the same amount of work either way. The verification happens even when no
    /// account matched, against <see cref="SignInDecoy"/>, because two identical sentences that
    /// arrive at measurably different times are not identical.
    /// </summary>
    public async Task<AccountView> Handle(SignIn command, CancellationToken ct)
    {
        var email = AccountCredentials.Normalise(command.Email);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

        var verified = hasher.Verify(
            account?.PasswordHash ?? SignInDecoy.For(hasher), command.Password);

        if (account is null || !verified)
        {
            throw new SignInRefusedException();
        }

        return AccountCredentials.ViewOf(account);
    }
}
