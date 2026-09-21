using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

// A camping session, as commands. The ones that move the whole table on are the DM's: the zone,
// how the campsite turned out, and breaking camp. The ones a player does for their own character
// are open to anybody with the code, like a camp activity and like an import: taking a Camping
// activity, choosing a meal, writing a recipe into the book, counting the larder.

public sealed record SetCampZone(string Code, string? DmKey, string? ZoneName, int ZoneDc, int EncounterDc)
    : IRequest<CampaignView>;

/// <summary>How the Prepare Campsite check went, and null to take the result back.</summary>
public sealed record RecordCampsite(string Code, string? DmKey, string? Outcome) : IRequest<CampaignView>;

/// <summary>
/// One character takes one Camping activity, and says how the check went.
/// <para>This is where the rules of step 2 are kept: two hours on the clock, four activities a
/// day each, none at all at a critically failed campsite, and an activity somebody has already
/// succeeded at waits for the next camp. The app does not roll the check. The table does, and
/// says what it got.</para>
/// </summary>
public sealed record TakeCampingActivity(
    string Code, string? DmKey, Guid CharacterId, string Activity, string Outcome) : IRequest<CampaignView>;

public sealed record SaveCampEntry(
    string Code, string? DmKey, Guid EntryId, string Kind, string Name, string Does, int? Dc)
    : IRequest<CampaignView>;

public sealed record RemoveCampEntry(string Code, string? DmKey, Guid EntryId) : IRequest<CampaignView>;

/// <summary>A null <paramref name="Kind"/> is nobody having decided yet and removes the choice, so
/// a mis-tap is not a loss and tapping the same choice twice is a clear.</summary>
public sealed record ChooseMeal(
    string Code, string? DmKey, Guid CharacterId, string? Kind, Guid? RecipeId, string? RuleId)
    : IRequest<CampaignView>;

public sealed record SetCampSupplies(string Code, string? DmKey, int BasicIngredients, int SpecialIngredients)
    : IRequest<CampaignView>;

/// <summary>Daily preparations are done and the party moves on: half an hour on the clock, and
/// the next camp starts from nothing but its zone, its book and its larder.</summary>
public sealed record BreakCamp(string Code, string? DmKey) : IRequest<CampaignView>;

static class CampRules
{
    public const string CodeMessage = "A campaign code is four to twelve letters and digits.";

    public static bool IsOutcome(string? outcome) => Enum.TryParse<CampOutcome>(outcome, out _);
}

public sealed class SetCampZoneValidator : AbstractValidator<SetCampZone>
{
    public SetCampZoneValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.ZoneName).MaximumLength(80);
        RuleFor(c => c.ZoneDc).InclusiveBetween(5, 60);

        // A flat check, so past 20 it cannot be met and below 2 it cannot be missed.
        RuleFor(c => c.EncounterDc).InclusiveBetween(2, 20);
    }
}

public sealed class RecordCampsiteValidator : AbstractValidator<RecordCampsite>
{
    public RecordCampsiteValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.Outcome).Must(CampRules.IsOutcome).When(c => c.Outcome is not null)
                               .WithMessage("An outcome is one of the four degrees of success.");
    }
}

public sealed class TakeCampingActivityValidator : AbstractValidator<TakeCampingActivity>
{
    public TakeCampingActivityValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Activity).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Outcome).Must(CampRules.IsOutcome)
                               .WithMessage("An outcome is one of the four degrees of success.");
    }
}

public sealed class SaveCampEntryValidator : AbstractValidator<SaveCampEntry>
{
    public SaveCampEntryValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.EntryId).NotEmpty();
        RuleFor(c => c.Kind).Must(kind => Enum.TryParse<CampEntryKind>(kind, out _))
                            .WithMessage("An entry is a Recipe or an Activity.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);

        // Room for a recipe's three outcomes and what it costs, and not for a chapter.
        RuleFor(c => c.Does).NotNull().MaximumLength(1500);
        RuleFor(c => c.Dc).InclusiveBetween(5, 60).When(c => c.Dc is not null);
    }
}

public sealed class RemoveCampEntryValidator : AbstractValidator<RemoveCampEntry>
{
    public RemoveCampEntryValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.EntryId).NotEmpty();
    }
}

public sealed class ChooseMealValidator : AbstractValidator<ChooseMeal>
{
    public ChooseMealValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Kind).Must(kind => kind is null || Enum.TryParse<MealKind>(kind, out _))
                            .WithMessage("A meal is Rations, a BasicMeal or a SpecialMeal.");

        // A special meal is one particular meal: either a recipe out of this table's camp book or
        // one of the ruleset's own, never both and never neither. Nothing else names a meal at all.
        RuleFor(c => c).Must(c => c.RecipeId is not null ^ c.RuleId is not null)
                       .When(c => c.Kind == nameof(MealKind.SpecialMeal))
                       .WithMessage("Say which special meal.");
        RuleFor(c => c).Must(c => c.RecipeId is null && c.RuleId is null)
                       .When(c => c.Kind != nameof(MealKind.SpecialMeal))
                       .WithMessage("Only a special meal names a meal.");

        // Taken on trust, the way TakeCampingActivity takes a ruleset name on trust: the ruleset
        // is a separate database this handler does not read, and a name that matches nothing there
        // shows up as a meal with no record rather than as a rule of camping broken.
        RuleFor(c => c.RuleId).Length(1, 80).When(c => c.RuleId is not null);
    }
}

public sealed class SetCampSuppliesValidator : AbstractValidator<SetCampSupplies>
{
    public SetCampSuppliesValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
        RuleFor(c => c.BasicIngredients).InclusiveBetween(0, 9999);
        RuleFor(c => c.SpecialIngredients).InclusiveBetween(0, 9999);
    }
}

public sealed class BreakCampValidator : AbstractValidator<BreakCamp>
{
    public BreakCampValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid).WithMessage(CampRules.CodeMessage);
    }
}

/// <summary>
/// Every camp command is the same four lines around one change to the camp, so they are one
/// handler over a function from the camp to the camp it becomes. What differs is who may, how
/// much time it takes, and the function.
/// </summary>
public sealed class CampHandlers(ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster) :
    IRequestHandler<SetCampZone, CampaignView>,
    IRequestHandler<RecordCampsite, CampaignView>,
    IRequestHandler<TakeCampingActivity, CampaignView>,
    IRequestHandler<SaveCampEntry, CampaignView>,
    IRequestHandler<RemoveCampEntry, CampaignView>,
    IRequestHandler<ChooseMeal, CampaignView>,
    IRequestHandler<SetCampSupplies, CampaignView>,
    IRequestHandler<BreakCamp, CampaignView>
{
    public Task<CampaignView> Handle(SetCampZone command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "the zone", dmOnly: "say where the camp is", 0, ct,
            (camp, _) => camp with
            {
                ZoneName = string.IsNullOrWhiteSpace(command.ZoneName) ? null : command.ZoneName.Trim(),
                ZoneDc = command.ZoneDc,
                EncounterDc = command.EncounterDc,
            });

    public Task<CampaignView> Handle(RecordCampsite command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "the campsite", dmOnly: "say how the campsite turned out", 0, ct,
            (camp, _) => camp with
            {
                Campsite = command.Outcome is { } outcome ? Enum.Parse<CampOutcome>(outcome) : null,
            });

    public Task<CampaignView> Handle(TakeCampingActivity command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "a Camping activity", dmOnly: null, CampSite.ActivityMinutes, ct,
            (camp, campaign) =>
            {
                var character = campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId)
                    ?? throw new CombatantNotFoundException("That character is not in this campaign.");

                if (camp.Refusal(character.Id, character.Name, command.Activity.Trim()) is { } refusal)
                {
                    throw new CampRuleException(refusal);
                }

                return camp.With(new CampingTake(
                    character.Id, command.Activity.Trim(), Enum.Parse<CampOutcome>(command.Outcome)));
            });

    public Task<CampaignView> Handle(SaveCampEntry command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "a camp book entry", dmOnly: null, 0, ct,
            (camp, _) =>
            {
                if (camp.Book.All(entry => entry.Id != command.EntryId) && camp.Book.Length >= CampSite.MostEntries)
                {
                    throw new CampRuleException(
                        $"The camp book holds {CampSite.MostEntries} entries. Take one out to make room.");
                }

                return camp.With(new CampEntry(
                    command.EntryId, Enum.Parse<CampEntryKind>(command.Kind),
                    command.Name.Trim(), command.Does.Trim(), command.Dc));
            });

    public Task<CampaignView> Handle(RemoveCampEntry command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "a camp book entry removed", dmOnly: null, 0, ct,
            (camp, _) => camp.Without(command.EntryId));

    public Task<CampaignView> Handle(ChooseMeal command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "a meal", dmOnly: null, 0, ct,
            (camp, campaign) =>
            {
                if (campaign.Characters.All(c => c.Id != command.CharacterId))
                {
                    throw new CombatantNotFoundException("That character is not in this campaign.");
                }

                if (command.Kind is null)
                {
                    return camp.WithoutMealFor(command.CharacterId);
                }

                if (command.RecipeId is { } recipe
                    && !camp.Book.Any(entry => entry.Id == recipe && entry.Kind is CampEntryKind.Recipe))
                {
                    throw new CampRuleException("That recipe is not in this campaign's camp book.");
                }

                var choice = new MealChoice(
                    command.CharacterId, Enum.Parse<MealKind>(command.Kind), command.RecipeId, command.RuleId);

                if (camp.MealRefusal(choice) is { } refusal)
                {
                    throw new CampRuleException(refusal);
                }

                return camp.With(choice);
            });

    public Task<CampaignView> Handle(SetCampSupplies command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "the larder", dmOnly: null, 0, ct,
            (camp, _) =>
            {
                // A larder below what tonight's meals have taken is a negative count, and the rules
                // have no such state.
                if (command.BasicIngredients < camp.BasicIngredientsSpent)
                {
                    throw new CampRuleException(
                        $"Tonight's meals have already taken {camp.BasicIngredientsSpent} basic ingredients out of the larder.");
                }

                return camp with
                {
                    BasicIngredients = command.BasicIngredients,
                    SpecialIngredients = command.SpecialIngredients,
                };
            });

    public Task<CampaignView> Handle(BreakCamp command, CancellationToken ct) =>
        Change(command.Code, command.DmKey, "breaking camp", dmOnly: "break camp",
            CampSite.DailyPreparationsMinutes, ct, (camp, _) => camp.BrokenCamp());

    async Task<CampaignView> Change(
        string code, string? dmKey, string label, string? dmOnly, int minutes, CancellationToken ct,
        Func<CampSite, Campaign, CampSite> next)
    {
        var change = await CampaignAccess.LoadForChangeAsync(db, undo, code, dmKey, label, ct);
        if (dmOnly is not null)
        {
            CampaignAccess.RequireDm(change.Role, dmOnly);
        }

        change.Campaign.Camp = next(change.Campaign.Camp, change.Campaign);
        change.Campaign.ElapsedMinutes += minutes;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

/// <summary>A rule of camping said no, and the message is the rule.</summary>
public sealed class CampRuleException(string message) : Exception(message);
