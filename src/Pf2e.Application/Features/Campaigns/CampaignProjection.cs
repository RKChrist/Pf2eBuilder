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
            campaign.ElapsedMinutes,
            campaign.Day,
            Of(campaign.Camp, campaign.Characters.Count));
    }

    static CampSiteView Of(CampSite camp, int partySize)
    {
        var (rest, watch) = Watches.For(partySize);

        return new CampSiteView(
            camp.Step.ToString(),
            camp.ZoneName,
            camp.ZoneDc,
            camp.EncounterDc,
            camp.EncounterDcTonight,
            camp.Campsite?.ToString(),
            camp.ActivitiesAllowed,
            camp.ActivityPenalty,
            [.. camp.Taken.Select(take => new CampingTakeView(take.CharacterId, take.Activity, take.Outcome.ToString()))],
            [.. camp.Meals.Select(meal => Of(camp, meal))],
            [.. camp.Book.Select(entry => new CampEntryView(entry.Id, entry.Kind.ToString(), entry.Name, entry.Does, entry.Dc))],
            camp.BasicIngredients,
            camp.SpecialIngredients,
            camp.BasicIngredientsLeft,
            rest,
            watch);
    }

    /// <summary>A camp book recipe's own words travel with the meal, so the character who ate it
    /// can read what it does without anybody looking the entry back up.</summary>
    static MealChoiceView Of(CampSite camp, MealChoice meal)
    {
        var recipe = camp.RecipeFor(meal);

        return new MealChoiceView(
            meal.CharacterId, meal.Kind.ToString(), meal.RecipeId, meal.RuleId, recipe?.Name, recipe?.Does);
    }

    /// <summary>The creatures a viewer may know anything about. For the DM that is everybody; for
    /// a player it is every character and only the monsters the DM has revealed. An unrevealed
    /// monster is still in a player's order, as a mob with a number, and that is decided where
    /// the row is drawn: it is never in this list, so nothing that reads this list can name it.</summary>
    static IEnumerable<Combatant> Visible(ViewerRole role, Encounter encounter) =>
        InOrder(encounter).Where(combatant => role is ViewerRole.Dm
                                              || combatant is not MonsterCombatant { Revealed: false });

    static IEnumerable<Combatant> InOrder(Encounter encounter) =>
        encounter.Order()
                 .Select(entry => encounter.Find(entry.Id))
                 .OfType<Combatant>();

    static EncounterView Of(
        ViewerRole role,
        Encounter encounter,
        Campaign campaign,
        IReadOnlyList<Combatant> visible,
        IReadOnlySet<Guid> visibleIds) => new(
        encounter.Round,
        // Everybody in the order has a row now, a mob included, so the marker can always say
        // whose turn it is. What it points at for a player is a row that says "Mob 2".
        encounter.CurrentCombatantId,
        [.. InOrder(encounter).Select(combatant =>
            role is ViewerRole.Player && combatant is MonsterCombatant { Revealed: false } hidden
                ? Mob(hidden, encounter)
                : Of(role, combatant, encounter, campaign))],
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
            role is ViewerRole.Dm && combatant is MonsterCombatant statted ? StatLine(statted, effects) : null);
    }

    /// <summary>
    /// All a player is given about a monster nobody has shown them: that something is there,
    /// what to call it, and where it acts. No name, no record, no traits, no numbers and no
    /// conditions, because a condition's name can say what the creature is.
    /// <para>The id is the real one. It is a random number that names nothing, and keeping it
    /// means the row does not change identity under a player when the DM reveals it.</para>
    /// </summary>
    static CombatantView Mob(MonsterCombatant monster, Encounter encounter) => new(
        monster.Id,
        monster.Kind.ToString(),
        monster.Alias,
        monster.Initiative,
        encounter.CurrentCombatantId == monster.Id,
        Revealed: false,
        [],
        Monster: null);

    /// <summary>
    /// A monster's numbers go through the same stacking rule a character's do, so frightened 1
    /// on a goblin is armour class 15 on the DM's screen and not a sum done in somebody's head
    /// while four people wait. The stat block's number is the base, taken whole, the way a
    /// weapon's bonus is: a creature's line is printed finished and has no parts to rebuild.
    /// </summary>
    static MonsterNumbersNow Now(MonsterCombatant monster, IEnumerable<ActiveEffect> effects)
    {
        var modifiers = effects.SelectMany(effect => effect.Modifiers()).ToList();
        int With(int stated, StatTarget target) => Stacking.Resolve(stated, target, modifiers).Total;

        return new MonsterNumbersNow(
            With(monster.Stats.ArmorClass, StatTarget.ArmorClass),
            With(monster.Stats.Fortitude, StatTarget.Fortitude),
            With(monster.Stats.Reflex, StatTarget.Reflex),
            With(monster.Stats.Will, StatTarget.Will),
            With(monster.Stats.Perception, StatTarget.Perception));
    }

    static MonsterStatLineView StatLine(MonsterCombatant monster, IEnumerable<ActiveEffect> effects) => new(
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
        [.. monster.Stats.Traits],
        monster.Alias,
        Now(monster, effects));

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
