using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// One operation applies, updates and removes, and it is idempotent: the client names the
/// application with a Guid it generates, so resending the same apply over a flaky phone
/// connection converges on the same state instead of stacking a duplicate. A null
/// <see cref="Effect"/> removes the application, and removing one that is not there succeeds.
/// <para>The targets are a list, so Rallying Anthem on the whole party is one row that reached
/// five creatures rather than five rows that happen to share a name.</para>
/// </summary>
public sealed record ApplyEffect(
    string Code,
    string? DmKey,
    Guid ApplicationId,
    EffectSpec? Effect,
    IReadOnlyList<EffectTargetSpec> Targets) : IRequest<CampaignView>;

public sealed class ApplyEffectValidator : AbstractValidator<ApplyEffect>
{
    public ApplyEffectValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");

        // An empty application id is a client that forgot to generate one, and every such client
        // would otherwise share a single application.
        RuleFor(c => c.ApplicationId).NotEmpty()
                                     .WithMessage("An effect needs an id the client made up.");

        When(c => c.Effect is not null, () =>
        {
            RuleFor(c => c.Effect!).SetValidator(new EffectSpecValidator());
            RuleFor(c => c.Targets).NotEmpty().WithMessage("An effect is applied to somebody.");
            RuleForEach(c => c.Targets).SetValidator(new EffectTargetSpecValidator());
        });
    }
}

public sealed class EffectTargetSpecValidator : AbstractValidator<EffectTargetSpec>
{
    public EffectTargetSpecValidator()
    {
        RuleFor(t => t.Kind).Must(kind => kind is "Character")
                            .WithMessage("Kind must be Character.");
        RuleFor(t => t.Id).NotNull().NotEqual(Guid.Empty)
                          .WithMessage("A named target needs the id of the creature it names.");
    }
}

public sealed class ApplyEffectHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<ApplyEffect, CampaignView>
{
    public async Task<CampaignView> Handle(ApplyEffect command, CancellationToken ct)
    {
        var (campaign, role) = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "an effect", ct);

        var existing = campaign.EffectApplications.FirstOrDefault(e => e.Id == command.ApplicationId);

        if (command.Effect is null)
        {
            if (existing is not null)
            {
                campaign.EffectApplications.Remove(existing);
            }
        }
        else
        {
            Write(campaign, command, existing);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A retry arriving twice at once is the case a client-named id exists to make safe,
            // and it is the case that breaks a find-then-insert: both requests see nothing and
            // both insert the same key. If the application now holds what this caller asked for
            // then this caller succeeded, whoever wrote the row. The request that did the
            // writing has already told the campaign, which is why nothing is broadcast here.
            var (settled, settledRole) =
                await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct, tracking: false);

            var present = settled.EffectApplications.Any(e => e.Id == command.ApplicationId);
            if (command.Effect is not null != present)
            {
                throw;
            }

            return CampaignProjection.For(settledRole, settled);
        }

        var view = CampaignProjection.For(role, campaign);
        foreach (var sheet in view.Characters)
        {
            await broadcaster.CharacterChangedAsync(campaign.Code, sheet, ct);
        }

        return view;
    }

    /// <summary>
    /// The insert and the update halves of the upsert, side by side, so an application that is
    /// already there and one that is not end up with the same columns. The target list is
    /// reconciled rather than appended to, because re-sending an apply with fewer targets means
    /// fewer targets and not the union of both attempts.
    /// </summary>
    static void Write(Campaign campaign, ApplyEffect command, EffectApplication? existing)
    {
        var spec = command.Effect!;
        var active = SheetViews.ToActive(command.ApplicationId, spec);
        var timing = spec.Timing is null
            ? DurationTiming.None
            : Enum.Parse<DurationTiming>(spec.Timing, ignoreCase: true);

        var application = existing ?? new EffectApplication
        {
            Id = command.ApplicationId,
            CampaignId = campaign.Id,
        };

        application.Overwrite(active, timing, spec.SourceCreatureId);
        application.PersistentDamage = spec.PersistentDamage;
        application.PersistentDamageType = spec.PersistentDamageType;

        var wanted = command.Targets
            .Select(target => (Kind: Enum.Parse<EffectTargetKind>(target.Kind, ignoreCase: true), Id: target.Id!.Value))
            .DistinctBy(target => target.Id)
            .ToList();

        // A target nothing in this campaign answers to would become a row nobody ever reads,
        // and the effect would look applied while doing nothing.
        foreach (var (_, id) in wanted)
        {
            if (campaign.Characters.All(c => c.Id != id) && campaign.Encounter?.Find(id) is null)
            {
                throw new CombatantNotFoundException($"Nothing in this campaign has the id {id}.");
            }
        }

        application.Targets.RemoveAll(target => wanted.All(w => w.Id != target.TargetId));

        foreach (var (kind, id) in wanted)
        {
            var target = application.Targets.FirstOrDefault(t => t.TargetId == id)
                         ?? Added(application, kind, id);

            target.Value = spec.Value;
            target.RemainingRounds = timing is DurationTiming.None ? null : spec.Rounds;
        }

        if (existing is null)
        {
            campaign.EffectApplications.Add(application);
        }
    }

    static EffectTarget Added(EffectApplication application, EffectTargetKind kind, Guid id)
    {
        var target = new EffectTarget { ApplicationId = application.Id, Kind = kind, TargetId = id };
        application.Targets.Add(target);
        return target;
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
