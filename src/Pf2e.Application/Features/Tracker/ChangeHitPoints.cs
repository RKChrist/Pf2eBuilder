using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Tracker;

/// <summary>A signed delta and never an absolute. Two people applying damage at once must sum,
/// and last-write-wins on an absolute silently loses one of them.</summary>
public sealed record ChangeHitPoints(string TableCode, Guid CharacterId, int Delta)
    : IRequest<CharacterSheetView?>;

public sealed class ChangeHitPointsValidator : AbstractValidator<ChangeHitPoints>
{
    public ChangeHitPointsValidator()
    {
        RuleFor(c => c.TableCode).Must(TableCode.IsValid)
                                 .WithMessage("A table code is four to twelve letters and digits.");
        RuleFor(c => c.Delta).NotEqual(0).WithMessage("A change of no hit points is not a change.");
        RuleFor(c => c.Delta).InclusiveBetween(-999, 999);
    }
}

public sealed class ChangeHitPointsHandler(ITrackerDbContext db, ITableBroadcaster broadcaster)
    : IRequestHandler<ChangeHitPoints, CharacterSheetView?>
{
    public async Task<CharacterSheetView?> Handle(ChangeHitPoints command, CancellationToken ct)
    {
        var code = TableCode.Normalize(command.TableCode);

        var table = await db.Tables
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        if (table?.Characters.FirstOrDefault(c => c.Id == command.CharacterId) is not { } character)
        {
            return null;
        }

        var max = CharacterSheet.Compute(character.ToBuild(), character.ToSession()).MaxHitPoints;
        character.CurrentHitPoints = HitPoints.AfterDelta(character.CurrentHitPoints, command.Delta, max);
        await db.SaveChangesAsync(ct);

        var sheet = SheetViews.Of(character);
        await broadcaster.CharacterChangedAsync(code, sheet, ct);
        return sheet;
    }
}
