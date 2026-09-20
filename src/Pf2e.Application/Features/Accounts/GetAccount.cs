using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Application.Features.Accounts;

/// <summary>
/// Reads the account a validated token names. It answers null rather than throwing, because its
/// one caller is the question "who is this browser" and an account that has since been deleted
/// is the same answer as no account at all: nobody. A refusal there would be a refusal of
/// something nobody asked for.
/// </summary>
public sealed record GetAccount(Guid Id) : IRequest<AccountView?>;

public sealed class GetAccountValidator : AbstractValidator<GetAccount>
{
    public GetAccountValidator()
    {
        RuleFor(q => q.Id).NotEmpty().WithMessage("An account is read by its id.");
    }
}

public sealed class GetAccountHandler(IAccountsDbContext db) : IRequestHandler<GetAccount, AccountView?>
{
    public async Task<AccountView?> Handle(GetAccount query, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking()
                                       .FirstOrDefaultAsync(a => a.Id == query.Id, ct);

        return account is null ? null : AccountCredentials.ViewOf(account);
    }
}
