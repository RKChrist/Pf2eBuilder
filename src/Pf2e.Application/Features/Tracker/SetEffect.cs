using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Tracker;

/// <summary>
/// One operation applies, updates and removes, and it is idempotent: the client names the slot
/// with a Guid it generates, so resending the same apply over a flaky table wifi converges on
/// the same state instead of stacking a duplicate. A null <see cref="Effect"/> empties the slot,
/// and emptying a slot nothing is in succeeds.
/// </summary>
public sealed record SetEffect(string TableCode, Guid CharacterId, Guid EffectId, EffectSpec? Effect)
    : IRequest<CharacterSheetView?>;

public sealed class SetEffectValidator : AbstractValidator<SetEffect>
{
    public SetEffectValidator()
    {
        RuleFor(c => c.TableCode).Must(TableCode.IsValid)
                                 .WithMessage("A table code is four to twelve letters and digits.");
        RuleFor(c => c.CharacterId).NotEmpty();

        // An empty slot id is a client that forgot to generate one, and every such client would
        // otherwise share a single slot.
        RuleFor(c => c.EffectId).NotEmpty().WithMessage("An effect slot needs an id the client made up.");

        RuleFor(c => c.Effect!).SetValidator(new EffectSpecValidator()).When(c => c.Effect is not null);
    }
}

public sealed class EffectSpecValidator : AbstractValidator<EffectSpec>
{
    const int MaxModifiers = 4;
    const int MaxValue = 10;

    public EffectSpecValidator()
    {
        RuleFor(e => e.Kind).Must(kind => kind is "Seeded" or "Rule" or "Custom")
                            .WithMessage("Kind must be Seeded, Rule or Custom.");
        RuleFor(e => e.Duration).MaximumLength(64).When(e => e.Duration is not null);

        When(e => e.Kind == "Seeded", () =>
        {
            RuleFor(e => e.Key).NotEmpty()
                               .Must(key => Domain.Conditions.Find(key!) is not null)
                               .WithMessage("No seeded effect has that key.");
            RuleFor(e => e.Value).Must(SuitsItsDefinition)
                                 .WithMessage($"An effect that carries a value needs one from 1 to {MaxValue}, " +
                                              "and one that carries none needs 0.");
            RuleFor(e => e.Modifiers).Empty()
                                     .WithMessage("A seeded effect takes its modifiers from the registry.");
        });

        When(e => e.Kind == "Rule", () =>
        {
            RuleFor(e => e.Key).NotEmpty().WithMessage("A rule effect names the rule it came from.");
            RuleFor(e => e.Modifiers).NotEmpty();
        });

        When(e => e.Kind == "Custom", () =>
        {
            RuleFor(e => e.Name).NotEmpty().WithMessage("A custom effect needs a name a player will recognise.");
            RuleFor(e => e.Key).Null().WithMessage("A custom effect comes from nowhere but the table.");
            RuleFor(e => e.Modifiers).NotEmpty()
                                     .Must(modifiers => modifiers.Count <= MaxModifiers)
                                     .WithMessage($"At most {MaxModifiers} modifiers.");
        });

        RuleForEach(e => e.Modifiers).SetValidator(new EffectModifierViewValidator())
                                     .When(e => e.Kind != "Seeded");
    }

    static bool SuitsItsDefinition(EffectSpec spec, int value) =>
        Domain.Conditions.Find(spec.Key ?? string.Empty) is not { } definition
        || (definition.HasValue ? value is >= 1 and <= MaxValue : value == 0);
}

public sealed class EffectModifierViewValidator : AbstractValidator<EffectModifierView>
{
    public EffectModifierViewValidator()
    {
        RuleFor(m => m.Type).Must(type => Enum.TryParse<ModifierType>(type, ignoreCase: true, out _))
                            .WithMessage("Type must be Circumstance, Item, Status or Untyped.");
        RuleFor(m => m.Value).NotEqual(0).WithMessage("A modifier worth nothing is not a modifier.");
        RuleFor(m => m.Value).InclusiveBetween(-10, 10);
        RuleFor(m => m.Applies).NotEmpty().WithMessage("A modifier applies to something.");
        RuleForEach(m => m.Applies).SetValidator(new SelectorSpecViewValidator());
    }
}

public sealed class SelectorSpecViewValidator : AbstractValidator<SelectorSpecView>
{
    public SelectorSpecViewValidator()
    {
        RuleFor(s => s.Kind).Must(kind => Enum.TryParse<SelectorKind>(kind, ignoreCase: true, out _))
                            .WithMessage("Kind must be Exactly, Governed, AllChecksAndDcs, SavingThrows or Speeds.");

        // Without these two the mapper would read an absent name as the first enum member, which
        // is a silently wrong selector rather than a refused one. A skill selector with no skill
        // name stays legal, because that is how "every skill" is written.
        RuleFor(s => s.Stat).NotNull()
                            .Must(stat => Enum.TryParse<StatKind>(stat, ignoreCase: true, out _))
                            .When(s => Named(s.Kind) is SelectorKind.Exactly);
        RuleFor(s => s.Attribute).NotNull()
                                 .Must(attribute => Enum.TryParse<AttributeKind>(attribute, ignoreCase: true, out _))
                                 .When(s => Named(s.Kind) is SelectorKind.Governed);
    }

    static SelectorKind? Named(string kind) =>
        Enum.TryParse<SelectorKind>(kind, ignoreCase: true, out var parsed) ? parsed : null;
}

public sealed class SetEffectHandler(ITrackerDbContext db, ITableBroadcaster broadcaster)
    : IRequestHandler<SetEffect, CharacterSheetView?>
{
    public async Task<CharacterSheetView?> Handle(SetEffect command, CancellationToken ct)
    {
        var code = TableCode.Normalize(command.TableCode);

        var table = await db.Tables
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        if (table?.Characters.FirstOrDefault(c => c.Id == command.CharacterId) is not { } character)
        {
            return null;
        }

        var slot = character.Effects.FirstOrDefault(e => e.Id == command.EffectId);

        if (command.Effect is null)
        {
            if (slot is not null)
            {
                character.Effects.Remove(slot);
            }
        }
        else
        {
            var active = SheetViews.ToActive(command.EffectId, command.Effect);
            if (slot is null)
            {
                character.Effects.Add(TrackedEffect.From(character.Id, active));
            }
            else
            {
                slot.Overwrite(active);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A retry arriving twice at once is the case a client-named slot exists to make
            // safe, and it is the case that breaks a find-then-insert: both requests see the
            // slot empty and both insert the same key. If the slot now holds what this caller
            // asked for then this caller succeeded, whoever wrote the row, so answer with what
            // is there. The request that did the writing has already told the table, which is
            // why nothing is broadcast here.
            var settled = await Current(code, command.CharacterId, ct);
            if (settled is null || (command.Effect is not null && settled.Effects.All(e => e.Id != command.EffectId)))
            {
                throw;
            }

            return settled;
        }

        var sheet = SheetViews.Of(character);
        await broadcaster.CharacterChangedAsync(code, sheet, ct);
        return sheet;
    }

    async Task<CharacterSheetView?> Current(string code, Guid characterId, CancellationToken ct)
    {
        var table = await db.Tables
            .AsNoTracking()
            .Include(t => t.Characters).ThenInclude(c => c.Effects)
            .SingleOrDefaultAsync(t => t.Code == code, ct);

        return table?.Characters.FirstOrDefault(c => c.Id == characterId) is { } character
            ? SheetViews.Of(character)
            : null;
    }
}
