using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Turning what the caller asked for into the creatures it reaches, and deciding whether they
/// were allowed to ask.
/// <para>Players apply their own buffs, with the DM watching it land. That is design/006's
/// choice for removing "who is tracking the +1" from the table, and it is why a player can
/// reach themselves and the party. Monsters are the DM's, individually and as a group, because
/// a player who can put frightened on a monster can also read the answer.</para>
/// </summary>
internal static class EffectTargets
{
    public const string Character = "Character";
    public const string Monster = "Monster";
    public const string AllPlayerCharacters = "AllPlayerCharacters";
    public const string AllMonsters = "AllMonsters";

    public static IReadOnlyList<string> Kinds { get; } =
        [Character, Monster, AllPlayerCharacters, AllMonsters];

    public static List<(EffectTargetKind Kind, Guid Id)> Resolve(
        Campaign campaign, ViewerRole role, IReadOnlyList<EffectTargetSpec> specs)
    {
        // Refused on what was asked for rather than on what it happened to resolve to. A player
        // asking for every monster in a fight that has none would otherwise succeed quietly and
        // learn nothing about the rule they just broke.
        if (role is not ViewerRole.Dm
            && specs.Any(spec => spec.Kind is Monster or AllMonsters))
        {
            throw new NotTheDmException("apply an effect to a monster");
        }

        var resolved = new List<(EffectTargetKind Kind, Guid Id)>();

        foreach (var spec in specs)
        {
            switch (spec.Kind)
            {
                case AllPlayerCharacters:
                    resolved.AddRange(campaign.Characters.Select(c => (EffectTargetKind.Character, c.Id)));
                    break;

                case AllMonsters:
                    resolved.AddRange(Monsters(campaign).Select(m => (EffectTargetKind.Monster, m.Id)));
                    break;

                default:
                    resolved.Add(One(campaign, spec));
                    break;
            }
        }

        return [.. resolved.DistinctBy(target => target.Id)];
    }

    /// <summary>
    /// The same gate on the way out, because an effect is two operations and authority over it
    /// is one question. A player who could lift frightened off the ogre would be deciding the
    /// fight as surely as one who put it there.
    /// </summary>
    public static void GuardRemoval(ViewerRole role, EffectApplication application)
    {
        if (role is not ViewerRole.Dm
            && application.Targets.Any(target => target.Kind is EffectTargetKind.Monster))
        {
            throw new NotTheDmException("remove an effect from a monster");
        }
    }

    static IEnumerable<MonsterCombatant> Monsters(Campaign campaign) =>
        campaign.Encounter?.Combatants.OfType<MonsterCombatant>() ?? [];

    /// <summary>
    /// The stated kind has to match what is actually there. A request naming a monster as a
    /// character is a client that has confused two ids, and answering it by quietly doing the
    /// other thing is how a player ends up buffing the ogre.
    /// </summary>
    static (EffectTargetKind Kind, Guid Id) One(Campaign campaign, EffectTargetSpec spec)
    {
        var id = spec.Id!.Value;
        var isCharacter = campaign.Characters.Any(c => c.Id == id);
        var isMonster = campaign.Encounter?.Find(id) is MonsterCombatant;

        return (spec.Kind, isCharacter, isMonster) switch
        {
            (Character, true, _) => (EffectTargetKind.Character, id),
            (Monster, _, true) => (EffectTargetKind.Monster, id),
            (_, false, false) => throw new CombatantNotFoundException(
                $"Nothing in this campaign has the id {id}."),
            _ => throw new CombatantNotFoundException(
                $"The id {id} is not a {spec.Kind.ToLowerInvariant()} in this campaign."),
        };
    }
}
