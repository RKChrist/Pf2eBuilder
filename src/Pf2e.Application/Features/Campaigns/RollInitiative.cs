using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Rolling initiative is what enters Encounter mode, which is what the document's state diagram
/// says. A combatant the request names takes the number the request gives, because at a real
/// table the players roll their own dice; anything it does not name is rolled here.
/// </summary>
public sealed record RollInitiative(string Code, string? DmKey, IReadOnlyList<InitiativeRoll> Rolls)
    : IRequest<CampaignView>;

public sealed class RollInitiativeValidator : AbstractValidator<RollInitiative>
{
    public RollInitiativeValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleForEach(c => c.Rolls).ChildRules(roll =>
        {
            roll.RuleFor(r => r.CombatantId).NotEmpty();
            roll.RuleFor(r => r.Initiative).InclusiveBetween(-20, 60);
        });
    }
}

public sealed class RollInitiativeHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RollInitiative, CampaignView>
{
    public async Task<CampaignView> Handle(RollInitiative command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "initiative", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "roll initiative");

        if (campaign.Encounter is not { Combatants.Count: > 0 } encounter)
        {
            throw new CombatantNotFoundException(
                "Nobody is in this encounter yet. Add the party and the monsters first.");
        }

        var given = command.Rolls.ToDictionary(roll => roll.CombatantId, roll => roll.Initiative);

        // Scout is the one activity that reaches everybody: one character ranging ahead hands
        // the whole party a circumstance bonus, so it is read once before anyone rolls.
        var scouted = Initiative.PartyBonus(campaign.Characters
            .Select(character => ExplorationActivities.Find(character.ExplorationActivity)));

        foreach (var combatant in encounter.Combatants)
        {
            combatant.Initiative = given.TryGetValue(combatant.Id, out var stated)
                ? stated
                : Rolled(campaign, combatant, scouted);
        }

        encounter.Round = 1;

        // What the party was doing when the fight started, for the things this engine states and
        // does not compute. A shield it does not know the bonus of is a reminder, not a guess.
        encounter.Reminders =
        [
            .. campaign.Characters
                .Where(character => ExplorationActivities.Find(character.ExplorationActivity)
                    is { Effect: InitiativeEffect.None } activity && activity.Key == "defend")
                .Select(character => new TurnReminder(
                    character.Id,
                    $"{character.Name} was Defending: their shield is already raised.")),
        ];

        // The marker is set from the sorted order, so the tie rule decides who goes first here
        // in exactly the same way it decides every later turn.
        encounter.CurrentCombatantId = encounter.Order()[0].Id;

        campaign.Mode = CampaignMode.Encounter;

        await db.SaveChangesAsync(ct);
        await broadcaster.ModeChangedAsync(
            campaign.Code, new CampaignModeView(campaign.Code, campaign.Mode.ToString()), ct);

        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }

    /// <summary>
    /// A d20 plus whatever the creature adds to it. What they add is decided by
    /// <see cref="Initiative.Modifier"/>, which is pure and tested without a die; this is only
    /// the die and the lookups that feed it.
    /// </summary>
    internal static int Rolled(Campaign campaign, Combatant combatant, int partyBonus = 0)
    {
        var die = RandomNumberGenerator.GetInt32(1, 21);

        if (combatant is MonsterCombatant monster)
        {
            return die + Initiative.Modifier(
                monster.Stats.Perception, null, _ => null, partyBonus, isAlly: false);
        }

        if (campaign.Characters.FirstOrDefault(c => c.Id == combatant.Id) is not { } character)
        {
            return die;
        }

        var sheet = CharacterSheet.Compute(
            character.ToBuild(), character.ToSession(campaign.EffectApplications));

        return die + Initiative.Modifier(
            sheet.Perception.Total,
            ExplorationActivities.Find(character.ExplorationActivity),
            named => sheet.Skills.FirstOrDefault(skill => skill.Name == named)?.Value.Total,
            partyBonus,
            isAlly: true);
    }
}
