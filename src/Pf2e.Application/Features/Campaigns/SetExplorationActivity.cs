using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// What a character is doing while the party travels. Null clears it.
/// <para>Not DM-only. A player chooses their own activity at a real table, and with no
/// per-player identity the most this app can enforce is "a participant may set a character's
/// activity", which is the same rule that already governs applying an effect to a character.</para>
/// <para><paramref name="DmKey"/> is carried all the same, and is not a permission here. It is
/// what the projection reads to decide which role it is answering, and passing null demoted the
/// DM's own screen to a player the moment they set somebody's activity: the mode switch and the
/// encounter controls vanished under them.</para>
/// </summary>
public sealed record SetExplorationActivity(string Code, string? DmKey, Guid CharacterId, string? Activity)
    : IRequest<CampaignView>;

public sealed class SetExplorationActivityValidator : AbstractValidator<SetExplorationActivity>
{
    public SetExplorationActivityValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Activity).Must(key => ExplorationActivities.Find(key) is not null)
                                .When(c => c.Activity is not null)
                                .WithMessage("That is not an exploration activity.");
    }
}

public sealed class SetExplorationActivityHandler(
    ITrackerDbContext db, IUndoStack undo, ICampaignBroadcaster broadcaster)
    : IRequestHandler<SetExplorationActivity, CampaignView>
{
    public async Task<CampaignView> Handle(SetExplorationActivity command, CancellationToken ct)
    {
        var change = await CampaignAccess.LoadForChangeAsync(
            db, undo, command.Code, command.DmKey, "exploration activity", ct);

        var character = change.Campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId)
            ?? throw new CombatantNotFoundException("That character is not in this campaign.");

        // Stored by key rather than by name, because the name is what the screen prints and the
        // key is what the engine matches, and renaming one must not break the other.
        character.ExplorationActivity = ExplorationActivities.Find(command.Activity)?.Key;

        await db.SaveChangesAsync(ct);
        return await CampaignAccess.PublishChangeAsync(broadcaster, undo, change, ct);
    }
}
