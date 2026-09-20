using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Reveal is one boolean and not three steps. Unrevealed means the monster is not in a player's
/// combatant list at all; revealed means a player sees its name, its initiative and its
/// conditions. Neither setting puts its hit points or its stat line in a player's payload.
/// </summary>
public sealed record RevealMonster(string Code, string? DmKey, Guid CombatantId, bool Revealed)
    : IRequest<CampaignView>;

public sealed class RevealMonsterValidator : AbstractValidator<RevealMonster>
{
    public RevealMonsterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();
    }
}

public sealed class RevealMonsterHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<RevealMonster, CampaignView>
{
    public async Task<CampaignView> Handle(RevealMonster command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a reveal", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "reveal a monster");

        if (campaign.Encounter?.Find(command.CombatantId) is not MonsterCombatant monster)
        {
            throw new CombatantNotFoundException("No monster in this encounter has that id.");
        }

        monster.Revealed = command.Revealed;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

/// <summary>
/// The DM changes a monster in the fight: its name, or any number on its line. An elite or weak
/// adjustment, a boss with more hit points than the book gives it, and a typo are all this.
/// <para>The whole line is sent and not a patch, so there is one shape to validate and what the
/// DM sees in the form is what the monster becomes. Hit points it currently has move by as much
/// as its maximum moved, so raising a fresh monster's maximum leaves it fresh and lowering a
/// wounded one's does not heal it.</para>
/// </summary>
public sealed record EditMonster(string Code, string? DmKey, Guid CombatantId, EditMonsterRequest Monster)
    : IRequest<CampaignView>;

public sealed class EditMonsterValidator : AbstractValidator<EditMonster>
{
    public EditMonsterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();
        RuleFor(c => c.Monster.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Monster.MaxHitPoints).InclusiveBetween(1, 9999);
        RuleFor(c => c.Monster.Level).InclusiveBetween(-1, 30);
        RuleFor(c => c.Monster.ArmorClass).InclusiveBetween(0, 99);
        RuleFor(c => c.Monster.Fortitude).InclusiveBetween(-10, 99);
        RuleFor(c => c.Monster.Reflex).InclusiveBetween(-10, 99);
        RuleFor(c => c.Monster.Will).InclusiveBetween(-10, 99);
        RuleFor(c => c.Monster.Perception).InclusiveBetween(-10, 99);
    }
}

public sealed class EditMonsterHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<EditMonster, CampaignView>
{
    public async Task<CampaignView> Handle(EditMonster command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a change to a monster", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "change a monster");

        if (campaign.Encounter?.Find(command.CombatantId) is not MonsterCombatant monster)
        {
            throw new CombatantNotFoundException("No monster in this encounter has that id.");
        }

        var wanted = command.Monster;
        var moved = wanted.MaxHitPoints - monster.Stats.MaxHitPoints;

        monster.Name = wanted.Name.Trim();
        monster.Stats = monster.Stats with
        {
            Level = wanted.Level,
            MaxHitPoints = wanted.MaxHitPoints,
            ArmorClass = wanted.ArmorClass,
            Fortitude = wanted.Fortitude,
            Reflex = wanted.Reflex,
            Will = wanted.Will,
            Perception = wanted.Perception,
        };
        monster.CurrentHitPoints = Math.Clamp(monster.CurrentHitPoints + moved, 0, wanted.MaxHitPoints);

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

/// <summary>
/// A monster the DM changed goes back to the numbers the ruleset prints for it.
/// <para>An edit writes over the copy this monster took when it joined, and the record it was
/// copied from is never touched, so the book's line is always there to be read again. A monster
/// the DM wrote has no book to go back to, and saying so is better than resetting it to
/// nothing.</para>
/// <para>The wound is kept, the same way an edit keeps it: hit points move by as much as the
/// maximum moved. An "Elite" or "Weak" the form put in front of the name comes off with the
/// numbers it described.</para>
/// </summary>
public sealed record ResetMonster(string Code, string? DmKey, Guid CombatantId) : IRequest<CampaignView>;

public sealed class ResetMonsterValidator : AbstractValidator<ResetMonster>
{
    public ResetMonsterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CombatantId).NotEmpty();
    }
}

public sealed class ResetMonsterHandler(
    ITrackerDbContext db, IRulesDbContext rules, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<ResetMonster, CampaignView>
{
    public async Task<CampaignView> Handle(ResetMonster command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "a monster put back", ct);
        var (campaign, role) = (change.Campaign, change.Role);
        CampaignAccess.RequireDm(role, "put a monster back");

        if (campaign.Encounter?.Find(command.CombatantId) is not MonsterCombatant monster)
        {
            throw new CombatantNotFoundException("No monster in this encounter has that id.");
        }

        if (monster.RuleId.Length == 0)
        {
            throw new CombatantNotFoundException(
                $"{monster.Name} is a monster of your own, so there is no book to put it back to.");
        }

        var (_, book) = await AddCombatantHandler.BookLineAsync(rules, monster.RuleId, ct);
        var moved = book.MaxHitPoints - monster.Stats.MaxHitPoints;

        monster.Stats = book;
        monster.CurrentHitPoints = Math.Clamp(monster.CurrentHitPoints + moved, 0, book.MaxHitPoints);
        foreach (var adjusted in new[] { "Elite ", "Weak " })
        {
            if (monster.Name.StartsWith(adjusted, StringComparison.OrdinalIgnoreCase))
            {
                monster.Name = monster.Name[adjusted.Length..];
            }
        }

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}

/// <summary>
/// The encounter ends and the campaign returns to Exploration, which is the other half of the
/// document's state diagram. The encounter and its combatants go with it; effects that outlast
/// the fight stay, because they belong to the campaign and not to the encounter.
/// </summary>
public sealed record EndEncounter(string Code, string? DmKey) : IRequest<CampaignView>;

public sealed class EndEncounterValidator : AbstractValidator<EndEncounter>
{
    public EndEncounterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
    }
}

public sealed class EndEncounterHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<EndEncounter, CampaignView>
{
    public async Task<CampaignView> Handle(EndEncounter command, CancellationToken ct)
    {
        // No snapshot. Ending the fight is where the undo stack is emptied, because the stack is
        // per encounter and there is nothing in a finished one left to reverse.
        var (campaign, role) = await CampaignAccess.LoadAsync(db, command.Code, command.DmKey, ct);
        CampaignAccess.RequireDm(role, "end the encounter");
        undo.Clear(campaign.Id);

        if (campaign.Encounter is { } encounter)
        {
            // Effects that reached a monster leave with the monster, because the creature they
            // were on no longer exists to carry them.
            var gone = encounter.Combatants.OfType<MonsterCombatant>().Select(m => m.Id).ToHashSet();
            foreach (var application in campaign.EffectApplications)
            {
                application.Targets.RemoveAll(target => gone.Contains(target.TargetId));
            }

            campaign.EffectApplications.RemoveAll(application => application.Targets.Count == 0);
            campaign.Encounter = null;
        }

        campaign.Mode = CampaignMode.Exploration;

        await db.SaveChangesAsync(ct);
        await broadcaster.ModeChangedAsync(
            campaign.Code, new CampaignModeView(campaign.Code, campaign.Mode.ToString()), ct);

        return await CampaignAccess.PublishAsync(broadcaster, campaign, role, ct);
    }
}
