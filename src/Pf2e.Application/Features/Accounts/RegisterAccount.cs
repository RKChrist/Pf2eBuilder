using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Accounts;
using Pf2e.Domain.Accounts;

namespace Pf2e.Application.Features.Accounts;

/// <summary>Refused because the address is taken, which is a state and not a fault: the request
/// was well formed and somebody simply got there first.</summary>
public sealed class EmailAlreadyRegisteredException(string email) : Exception(
    $"There is already an account for {email}. Sign in with it instead, or use another address.");

/// <summary>
/// Brings an account into being. It answers with the same view a sign-in does, because the API
/// signs the new account in on the way out and the caller should not have to ask a second time
/// what it already knows.
/// </summary>
public sealed record RegisterAccount(string Email, string DisplayName, string Password)
    : IRequest<AccountView>;

public sealed class RegisterAccountValidator : AbstractValidator<RegisterAccount>
{
    public RegisterAccountValidator()
    {
        RuleFor(c => c.Email).Must(AccountCredentials.IsEmail)
                             .WithMessage("An email address is a name, an @ and a domain.");
        RuleFor(c => c.Email).Must(AccountCredentials.FitsInAnAddressField)
                             .WithMessage($"An email address is at most {AccountCredentials.MaxEmail} characters.");

        RuleFor(c => c.DisplayName).NotEmpty()
                                   .WithMessage("A display name is what the table will call you.")
                                   .MaximumLength(AccountCredentials.MaxDisplayName);

        // Length and nothing else. A passphrase beats a short password with a symbol in it, and
        // composition rules are what push people towards the short one with the symbol.
        RuleFor(c => c.Password)
            .Must(password => password is not null
                              && password.Length >= AccountCredentials.MinPassword
                              && password.Length <= AccountCredentials.MaxPassword)
            .WithMessage($"A password is {AccountCredentials.MinPassword} to " +
                         $"{AccountCredentials.MaxPassword} characters. There is no rule about " +
                         "which characters.");
    }
}

public sealed class RegisterAccountHandler(IAccountsDbContext db, IPasswordHasher hasher)
    : IRequestHandler<RegisterAccount, AccountView>
{
    public async Task<AccountView> Handle(RegisterAccount command, CancellationToken ct)
    {
        var email = AccountCredentials.Normalise(command.Email);

        if (await db.Accounts.AnyAsync(a => a.Email == email, ct))
        {
            throw new EmailAlreadyRegisteredException(email);
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = command.DisplayName.Trim(),
            PasswordHash = hasher.Hash(command.Password),
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two registrations of one address at the same moment both see nothing above and
            // both insert, and the unique index is what actually settles it. So the loser is
            // told the address is taken rather than handed a 500 that reads as our fault.
            //
            // Asked rather than assumed, though. A locked database and one nobody has migrated
            // arrive here too, and answering those with "that address is taken" is a lie that
            // sends somebody looking for an account that does not exist while the real fault
            // goes unreported. If the address is not there, this was not that.
            if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Email == email, ct))
            {
                throw;
            }

            throw new EmailAlreadyRegisteredException(email);
        }

        return AccountCredentials.ViewOf(account);
    }
}
