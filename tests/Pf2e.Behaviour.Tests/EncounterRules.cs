using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// The initiative order and the duration clock are functions over values, so everything the
/// document says about timing is asserted here rather than through a handler and a database.
/// </summary>
public class EncounterRules
{
    static readonly Guid Ogre = new("00000000-0000-0000-0000-0000000000A1");
    static readonly Guid Bard = new("00000000-0000-0000-0000-0000000000B1");
    static readonly Guid Fighter = new("00000000-0000-0000-0000-0000000000B2");

    static TurnEntry Adversary(Guid id, int initiative) => new(id, initiative, CombatantKind.Adversary);

    static TurnEntry Player(Guid id, int initiative) => new(id, initiative, CombatantKind.PlayerCharacter);

    // Player Core: a tie in initiative puts adversaries before player characters. It decides
    // whether the ogre swings before the bard sings on the round they rolled the same number,
    // which is the difference between a dead bard and a buffed party.
    [Fact]
    public void ATieInInitiativePutsTheAdversaryFirst()
    {
        var order = TurnOrder.Sort([Player(Bard, 21), Adversary(Ogre, 21)]);

        Assert.Equal([Ogre, Bard], order.Select(entry => entry.Id));
    }

    [Fact]
    public void AHigherInitiativeStillWinsWhateverTheKind()
    {
        var order = TurnOrder.Sort([Adversary(Ogre, 19), Player(Bard, 21)]);

        Assert.Equal([Bard, Ogre], order.Select(entry => entry.Id));
    }

    [Fact]
    public void TheOrderWrapsAndSaysSoSoTheRoundCanCount()
    {
        var order = TurnOrder.Sort([Adversary(Ogre, 23), Player(Bard, 21)]);

        var toBard = TurnOrder.Advance(order, Ogre);
        var wrapped = TurnOrder.Advance(order, Bard);

        Assert.Equal(new TurnAdvance(Ogre, Bard, NewRound: false), toBard);
        Assert.Equal(new TurnAdvance(Bard, Ogre, NewRound: true), wrapped);
    }

    // The marker is a combatant id and not an index. A reinforcement arriving mid-fight lands
    // wherever its initiative puts it and moves every index after it; with an index the turn
    // would silently jump to a different creature.
    [Fact]
    public void ACombatantAddedMidFightDoesNotMoveTheTurnOntoTheWrongCreature()
    {
        var before = TurnOrder.Sort([Adversary(Ogre, 23), Player(Bard, 21), Player(Fighter, 12)]);
        Assert.Equal(1, before.Select(entry => entry.Id).ToList().IndexOf(Bard));

        var reinforcement = Guid.NewGuid();
        var after = TurnOrder.Sort(
            [Adversary(Ogre, 23), Adversary(reinforcement, 22), Player(Bard, 21), Player(Fighter, 12)]);

        // Bard is index 1 before and index 2 after, so an index-based marker standing on the
        // bard would now be standing on the reinforcement.
        Assert.Equal(2, after.Select(entry => entry.Id).ToList().IndexOf(Bard));
        Assert.Equal(reinforcement, after[1].Id);

        Assert.Equal(Fighter, TurnOrder.Advance(after, Bard)!.Value.Starting);
    }

    [Fact]
    public void AMarkerNamingSomebodyWhoHasLeftStartsTheOrderAgain()
    {
        var order = TurnOrder.Sort([Adversary(Ogre, 23), Player(Bard, 21)]);

        var advance = TurnOrder.Advance(order, Guid.NewGuid());

        Assert.Equal(new TurnAdvance(null, Ogre, NewRound: false), advance);
        Assert.Null(TurnOrder.Advance([], Ogre));
    }

    // A duration in rounds counts down at the start of the source's turn, so a one-round effect
    // ends at the start of the caster's next turn and not a moment earlier.
    [Fact]
    public void AOneRoundEffectEndsAtTheStartOfItsCastersNextTurn()
    {
        var applications = new List<EffectApplication>
        {
            Rounds("Rallying Anthem", source: Bard, target: Fighter, rounds: 1),
        };

        TurnClock.Run(new TurnTick(Bard, Ogre, 1), applications, Name);
        Assert.Single(applications);
        Assert.Equal(1, applications[0].Targets.Single().RemainingRounds);

        TurnClock.Run(new TurnTick(Ogre, Bard, 2), applications, Name);
        Assert.Empty(applications);
    }

    [Fact]
    public void UntilTheStartOfYourNextTurnIsTiedToTheAffectedCreature()
    {
        var shield = new List<EffectApplication>
        {
            Rounds("Shield", source: Fighter, target: Fighter, rounds: 1,
                   timing: DurationTiming.TargetTurnStart),
        };

        TurnClock.Run(new TurnTick(Fighter, Ogre, 1), shield, Name);
        Assert.Single(shield);

        TurnClock.Run(new TurnTick(Ogre, Fighter, 2), shield, Name);
        Assert.Empty(shield);
    }

    // Frightened drops by 1 at the end of the affected creature's turn, which is a different
    // moment from every other countdown and the one tables forget.
    [Fact]
    public void FrightenedDropsAtTheEndOfTheAffectedCreaturesTurnAndNotAnyoneElses()
    {
        var applications = new List<EffectApplication> { Frightened(Fighter, 2) };

        TurnClock.Run(new TurnTick(Bard, Ogre, 1), applications, Name);
        Assert.Equal(2, applications[0].Targets.Single().Value);

        TurnClock.Run(new TurnTick(Fighter, Bard, 1), applications, Name);
        Assert.Equal(1, applications[0].Targets.Single().Value);

        TurnClock.Run(new TurnTick(Fighter, Bard, 2), applications, Name);
        Assert.Empty(applications);
    }

    // One application on two creatures is one row, and each creature's frightened has to fall
    // on its own turn. A value on the application would drop both at once.
    [Fact]
    public void OnePartyWideFrightenedIsOneRowAndTwoSeparateCountdowns()
    {
        var applications = new List<EffectApplication> { Frightened(Fighter, 2, also: Bard) };

        TurnClock.Run(new TurnTick(Fighter, Bard, 1), applications, Name);

        var row = Assert.Single(applications);
        Assert.Equal(1, row.Targets.Single(t => t.TargetId == Fighter).Value);
        Assert.Equal(2, row.Targets.Single(t => t.TargetId == Bard).Value);
    }

    [Fact]
    public void PersistentDamageIsRemindedAtTheEndOfTheAffectedCreaturesTurnAndNeverApplied()
    {
        var burning = new EffectApplication
        {
            Id = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            Name = "Persistent fire",
            SourceKind = "Custom",
            PersistentDamage = 4,
            PersistentDamageType = "fire",
        };
        burning.Targets.Add(Target(burning.Id, Ogre));

        var applications = new List<EffectApplication> { burning };

        Assert.Empty(TurnClock.Run(new TurnTick(Bard, Fighter, 1), applications, Name));

        var reminder = Assert.Single(TurnClock.Run(new TurnTick(Ogre, Bard, 1), applications, Name));
        Assert.Equal(Ogre, reminder.CreatureId);
        Assert.Contains("4 fire persistent damage", reminder.Text);
        Assert.Contains("DC 15 flat check", reminder.Text);

        // Still there, because nothing in this app rolls the flat check that ends it.
        Assert.Single(applications);
    }

    // The engine used to exclude armour class from "all your checks and DCs", which made a
    // frightened creature harder to hit than the rules say.
    [Fact]
    public void FrightenedLowersArmourClassBecauseArmourClassIsADc()
    {
        var frightened = Conditions.Frightened.ModifiersAt(2);

        Assert.All(frightened, modifier => Assert.True(modifier.AppliesTo(StatTarget.ArmorClass)));
    }

    static string Name(Guid id) =>
        id == Ogre ? "Ogre" : id == Bard ? "Zuz" : id == Fighter ? "Rune" : "Somebody";

    static EffectTarget Target(Guid applicationId, Guid targetId, int value = 0, int? rounds = null) =>
        new()
        {
            ApplicationId = applicationId,
            Kind = EffectTargetKind.Character,
            TargetId = targetId,
            Value = value,
            RemainingRounds = rounds,
        };

    static EffectApplication Rounds(
        string name, Guid source, Guid target, int rounds,
        DurationTiming timing = DurationTiming.SourceTurnStart)
    {
        var application = new EffectApplication
        {
            Id = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            Name = name,
            SourceKind = "Custom",
            Timing = timing,
            SourceCreatureId = source,
        };

        application.Targets.Add(Target(application.Id, target, rounds: rounds));
        return application;
    }

    static EffectApplication Frightened(Guid target, int value, Guid? also = null)
    {
        var application = new EffectApplication
        {
            Id = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            Name = "Frightened",
            SourceKind = "Seeded",
            SourceKey = TurnClock.FrightenedKey,
        };

        application.Targets.Add(Target(application.Id, target, value));
        if (also is { } second)
        {
            application.Targets.Add(Target(application.Id, second, value));
        }

        return application;
    }
}
