using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Tracker;

public sealed record GetTable(string Code) : IRequest<TableView>;

public sealed class GetTableValidator : AbstractValidator<GetTable>
{
    public GetTableValidator()
    {
        RuleFor(q => q.Code).Must(TableCode.IsValid)
                            .WithMessage("A table code is four to twelve letters and digits.");
    }
}

public sealed class GetTableHandler(ITrackerDbContext db) : IRequestHandler<GetTable, TableView>
{
    public async Task<TableView> Handle(GetTable query, CancellationToken ct)
    {
        var code = TableCode.Normalize(query.Code);

        var table = await db.Tables
            .AsNoTracking()
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        // Anyone with a code is at that table, so a code nobody has imported into is a table
        // waiting to be started rather than a missing one.
        return table is null
            ? new TableView(code, false, [])
            : new TableView(code, true,
                [.. table.Characters
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(SheetViews.Of)]);
    }
}
