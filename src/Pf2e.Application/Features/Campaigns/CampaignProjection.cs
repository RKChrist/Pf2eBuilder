using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// The one function that turns a campaign and the role of whoever is asking into the value that
/// role is allowed to have. Every read and every command answers through here.
/// <para>One function and not a filter per handler. A filter per handler is a rule written down
/// as many times as there are handlers, and the second place is the one that leaks.</para>
/// <para>What the boundary is, in full:</para>
/// <list type="bullet">
/// <item>An unrevealed monster is not in a player's combatant list at all. It is dropped rather
/// than blanked, because a blanked row still says a monster is there.</item>
/// <item>A revealed monster gives a player its name, its initiative and its conditions.</item>
/// <item>A monster's hit points and stat line are never in a player's payload, at either reveal
/// setting. They live in one nullable field that this function only ever populates for the DM,
/// and the document's own default for how much health a player sees is nothing, so there is no
/// band and no fraction here either.</item>
/// <item>A reminder naming a creature a player cannot see is not in their copy, because the
/// reminder carries the name.</item>
/// </list>
/// </summary>
internal static class CampaignProjection
{
    public static CampaignView For(ViewerRole role, Campaign campaign)
    {
        var visible = campaign.Encounter is { } encounter
            ? Visible(role, encounter).ToList()
            : [];

        var visibleIds = visible.Select(combatant => combatant.Id).ToHashSet();

        return new CampaignView(
            campaign.Code,
            campaign.Mode.ToString(),
            role.ToString(),
            [.. campaign.Characters
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(character => SheetViews.Of(character, campaign.EffectApplications, campaign.ElapsedMinutes))],
            [.. campaign.EffectApplications
                .Select(application => Of(role, application, campaign, visibleIds))
                .OfType<EffectApplicationView>()],
            campaign.Encounter is { } present ? Of(role, present, campaign, visible, visibleIds) : null,
            campaign.ElapsedMinutes);
    }

    /// <summary>The list a viewer is allowed to know exists. For the DM that is everybody; for a
    /// player it is every character and only the monsters the DM has revealed.</summary>
    static IEnumerable<Combatant> Visible(ViewerRole role, Encounter encounter) =>
        encounter.Order()
                 .Select(entry => encounter.Find(entry.Id))
                 .OfType<Combatant>()
                 .Where(combatant => role is ViewerRole.Dm
                                     || combatant is not MonsterCombatant { Revealed: false });

    static EncounterView Of(
        ViewerRole role,
        Encounter encounter,
        Campaign campaign,
        IReadOnlyList<Combatant> visible,
        IReadOnlySet<Guid> visibleIds) => new(
        encounter.Round,
        // A player whose turn marker names a monster they cannot see is told only that it is
        // not their turn, because the id alone would say a hidden creature is acting.
        encounter.CurrentCombatantId is { } current && visibleIds.Contains(current) ? current : null,
        [.. visible.Select(combatant => Of(role, combatant, encounter, campaign))],
        [.. encounter.Reminders
            .Where(reminder => visibleIds.Contains(reminder.CreatureId))
            .Select(reminder => reminder.Text)]);

    static CombatantView Of(ViewerRole role, Combatant combatant, Encounter encounter, Campaign campaign)
    {
        var effects = AppliedEffects.On(combatant.Id, campaign.EffectApplications);

        return new CombatantView(
            combatant.Id,
            combatant.Kind.ToString(),
            combatant switch
            {
                MonsterCombatant monster => monster.Name,
                _ => campaign.Characters.FirstOrDefault(c => c.Id == combatant.Id)?.Name ?? "A character",
            },
            combatant.Initiative,
            encounter.CurrentCombatantId == combatant.Id,
            combatant is not MonsterCombatant monsterRevealed || monsterRevealed.Revealed,
            [.. effects.Select(SheetViews.Of)],
            // The only place a monster's numbers reach a payload, and the only condition that
            // lets them. A player projection cannot reach this expression.
            role is ViewerRole.Dm && combatant is MonsterCombatant statted ? StatLine(statted) : null);
    }

    static MonsterStatLineView StatLine(MonsterCombatant monster) => new(
        monster.CurrentHitPoints,
        monster.Stats.MaxHitPoints,
        monster.TemporaryHitPoints,
        monster.Stats.Level,
        monster.Stats.ArmorClass,
        monster.Stats.Fortitude,
        monster.Stats.Reflex,
        monster.Stats.Will,
        monster.Stats.Perception,
        monster.RuleId,
        [.. monster.Stats.Traits]);

    /// <summary>
    /// An application reaching a creature the viewer cannot see loses that target, and one that
    /// reached nobody they can see is dropped entirely. Otherwise the party-wide buff strip
    /// would count the monsters nobody has been shown.
    /// </summary>
    static EffectApplicationView? Of(
        ViewerRole role, EffectApplication application, Campaign campaign, IReadOnlySet<Guid> visibleIds)
    {
        var view = SheetViews.Of(application);

        if (role is ViewerRole.Dm)
        {
            return view;
        }

        var characters = campaign.Characters.Select(c => c.Id).ToHashSet();
        var allowed = view.Targets
            .Where(target => characters.Contains(target.Id) || visibleIds.Contains(target.Id))
            .ToList();

        return allowed.Count == 0 ? null : view with { Targets = allowed };
    }
}
