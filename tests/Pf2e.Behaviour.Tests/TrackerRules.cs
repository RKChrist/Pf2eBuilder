using System.Collections.Immutable;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Behaviour.Tests;

/// <summary>The level 7 Goblin Bard the rest of the suite is pinned to.</summary>
static class Gnibbo
{
    public const int MaxHitPoints = 76;

    public static Character Build { get; } = new(
        Name: "Gnibbo",
        Level: 7,
        ClassName: "Bard",
        AncestryName: "Goblin",
        KeyAttribute: AttributeKind.Charisma,
        Attributes: new AttributeModifiers(
            Strength: Ability.Modifier(10),
            Dexterity: Ability.Modifier(16),
            Constitution: Ability.Modifier(14),
            Intelligence: Ability.Modifier(12),
            Wisdom: Ability.Modifier(12),
            Charisma: Ability.Modifier(19)),
        Fortitude: ProficiencyRank.Trained,
        Reflex: ProficiencyRank.Trained,
        Will: ProficiencyRank.Expert,
        Perception: ProficiencyRank.Expert,
        ClassDc: ProficiencyRank.Trained,
        ArmorRank: ProficiencyRank.Trained,
        ArmorName: "+1 Studded Leather Armor",
        ArmorItemBonus: 3,
        ArmorDexCap: 3,
        AncestryHitPoints: 6,
        ClassHitPoints: 8,
        BonusHitPoints: 0,
        BonusHitPointsPerLevel: 0);

    public static Sheet SheetWith(params ActiveEffect[] effects) =>
        CharacterSheet.Compute(Build, SessionState.Fresh(MaxHitPoints) with { Effects = [.. effects] });

    public static ActiveEffect Condition(string key, int value = 0) =>
        new(Guid.NewGuid(), Conditions.Find(key)?.Name ?? key, value, new EffectSource.Seeded(key));

    public static ActiveEffect Custom(string name, ModifierType type, int value, params SelectorSpec[] applies) =>
        new(Guid.NewGuid(), name, 0, new EffectSource.Custom([new EffectModifier(type, value, [.. applies])]));

    public static TrackedCharacter Stored() =>
        TrackedCharacter.From(Guid.NewGuid(), Build, SessionState.Fresh(MaxHitPoints));
}

public class TrackerRules
{
    static SelectorSpec On(StatKind stat) => new(SelectorKind.Exactly, stat);

    [Fact]
    public void ClumsyReachesArmorClassAndReflexButNotWill()
    {
        var healthy = Gnibbo.SheetWith();
        var clumsy = Gnibbo.SheetWith(Gnibbo.Condition("clumsy", 2));

        Assert.Equal(healthy.ArmorClass.Total - 2, clumsy.ArmorClass.Total);
        Assert.Equal(healthy.Reflex.Total - 2, clumsy.Reflex.Total);
        Assert.Equal(healthy.Will.Total, clumsy.Will.Total);

        // Reading the breakdown rather than the total is what proves clumsy moved the number,
        // and not some other adjustment that happens to be worth the same.
        Assert.Contains(clumsy.Reflex.Applied, m => m.Source == "Clumsy 2" && m.Value == -2);
        Assert.DoesNotContain(clumsy.Will.Applied, m => m.Source == "Clumsy 2");
    }

    [Fact]
    public void FrightenedReachesPerceptionAndClassDc()
    {
        var healthy = Gnibbo.SheetWith();
        var frightened = Gnibbo.SheetWith(Gnibbo.Condition("frightened", 2));

        Assert.Equal(healthy.Perception.Total - 2, frightened.Perception.Total);
        Assert.Equal(healthy.ClassDc.Total - 2, frightened.ClassDc.Total);
        Assert.Contains(frightened.ClassDc.Applied, m => m.Source == "Frightened 2");
    }

    [Fact]
    public void DrainedCostsItsValueTimesLevelInMaximumHitPoints()
    {
        var drained = Gnibbo.SheetWith(Gnibbo.Condition("drained", 2));

        Assert.Equal(Gnibbo.MaxHitPoints - 14, drained.MaxHitPoints);

        // Drained costs hit points and penalises Con-based checks; it does not lower the
        // attribute, so the maximum is computed from the build's Constitution either way.
        Assert.Equal(Gnibbo.SheetWith().Fortitude.Total - 2, drained.Fortitude.Total);
    }

    [Fact]
    public void TwoHitPointDeltasInSequenceSum()
    {
        var afterFirst = HitPoints.AfterDelta(Gnibbo.MaxHitPoints, -10, Gnibbo.MaxHitPoints);
        var afterBoth = HitPoints.AfterDelta(afterFirst, -7, Gnibbo.MaxHitPoints);

        Assert.Equal(Gnibbo.MaxHitPoints - 17, afterBoth);
        Assert.NotEqual(HitPoints.AfterDelta(Gnibbo.MaxHitPoints, -7, Gnibbo.MaxHitPoints), afterBoth);
    }

    [Fact]
    public void HitPointsNeverFallBelowZeroOrRiseAboveTheMaximum()
    {
        Assert.Equal(0, HitPoints.AfterDelta(Gnibbo.MaxHitPoints, -999, Gnibbo.MaxHitPoints));
        Assert.Equal(Gnibbo.MaxHitPoints, HitPoints.AfterDelta(Gnibbo.MaxHitPoints, 20, Gnibbo.MaxHitPoints));
    }

    [Fact]
    public void AStatusPenaltyAndACircumstancePenaltyBothReachArmorClass()
    {
        var sheet = Gnibbo.SheetWith(Gnibbo.Condition("clumsy", 2), Gnibbo.Condition("off-guard"));

        Assert.Equal(Gnibbo.SheetWith().ArmorClass.Total - 4, sheet.ArmorClass.Total);
        Assert.Contains(sheet.ArmorClass.Applied, m => m.Source == "Clumsy 2" && m.Type == ModifierType.Status);
        Assert.Contains(sheet.ArmorClass.Applied, m => m.Source == "Off-Guard" && m.Type == ModifierType.Circumstance);
        Assert.Empty(sheet.ArmorClass.Suppressed);
    }

    [Fact]
    public void ACustomEffectReachesTheStackingRuleExactlyAsASeededOneDoes()
    {
        var anthem = Gnibbo.Custom("Courageous Anthem", ModifierType.Status, 1,
            On(StatKind.Will), On(StatKind.Attack));

        var sheet = Gnibbo.SheetWith(anthem);

        Assert.Equal(Gnibbo.SheetWith().Will.Total + 1, sheet.Will.Total);
        Assert.Contains(sheet.Will.Applied, m => m.Source == "Courageous Anthem" && m.Type == ModifierType.Status);

        // The sheet carries no attack roll yet, so the effect's own modifiers are resolved
        // against the target directly to show it is the same one path.
        var attack = Stacking.Resolve(13, StatTarget.Attack(AttributeKind.Dexterity), anthem.Modifiers());
        Assert.Equal(14, attack.Total);
    }

    [Fact]
    public void TwoStatusBonusesDoNotStackAndTheSmallerIsVisiblySuppressed()
    {
        var sheet = Gnibbo.SheetWith(
            Gnibbo.Custom("Heroism", ModifierType.Status, 2, On(StatKind.Will)),
            Gnibbo.Custom("Courageous Anthem", ModifierType.Status, 1, On(StatKind.Will)));

        Assert.Equal(Gnibbo.SheetWith().Will.Total + 2, sheet.Will.Total);
        Assert.Contains(sheet.Will.Applied, m => m.Source == "Heroism");

        var loser = Assert.Single(sheet.Will.Suppressed);
        Assert.Equal("Courageous Anthem", loser.Modifier.Source);
        Assert.Contains("does not stack", loser.Reason);
        Assert.Contains("Heroism", loser.Reason);
    }

    [Fact]
    public void ProvenanceChangesTheLabelAndNothingElse()
    {
        ImmutableArray<EffectModifier> modifiers =
            [new EffectModifier(ModifierType.Status, 1, [On(StatKind.Will)])];

        var fromRule = new ActiveEffect(Guid.NewGuid(), "Courageous Anthem", 0,
            new EffectSource.Rule("spell-courageous-anthem", modifiers));
        var fromPlayer = new ActiveEffect(Guid.NewGuid(), "Courageous Anthem", 0,
            new EffectSource.Custom(modifiers));

        var byRule = Gnibbo.SheetWith(fromRule).Will;
        var byPlayer = Gnibbo.SheetWith(fromPlayer).Will;

        Assert.Equal(byRule.Total, byPlayer.Total);
        Assert.Equal(
            byRule.Applied.Select(m => (m.Source, m.Type, m.Value)),
            byPlayer.Applied.Select(m => (m.Source, m.Type, m.Value)));
    }

    [Fact]
    public void AnEffectWhoseSeededKeyNoLongerResolvesIsSkippedRatherThanThrownOn()
    {
        var stale = Gnibbo.Condition("bewildered", 3);
        var sheet = Gnibbo.SheetWith(stale);

        Assert.Equal(Gnibbo.SheetWith().ArmorClass.Total, sheet.ArmorClass.Total);
        Assert.Empty(stale.Modifiers());
        Assert.Equal(stale, Assert.Single(sheet.Effects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void AValuedEffectStoredBelowOneStillComputes(int stored)
    {
        var sheet = Gnibbo.SheetWith(Gnibbo.Condition("clumsy", stored));

        Assert.Equal(Gnibbo.SheetWith().ArmorClass.Total - 1, sheet.ArmorClass.Total);
    }

    [Theory]
    [InlineData("abcd", true)]
    [InlineData(" gnib7 ", true)]
    [InlineData("abc", false)]
    [InlineData("abcdefghijklm", false)]
    [InlineData("ab-cd", false)]
    public void ACampaignCodeIsFourToTwelveLettersAndDigits(string code, bool valid)
    {
        Assert.Equal(valid, CampaignCode.IsValid(code));
    }

    [Fact]
    public void NormalizingACampaignCodeUppercasesAndTrimsSoTwoTypingsAreOneCampaign()
    {
        Assert.Equal("GNIB7", CampaignCode.Normalize(" gnib7 "));
    }

    [Fact]
    public void ApplyingANewBuildLeavesTheSessionWhereItWas()
    {
        var stored = Gnibbo.Stored();
        stored.CurrentHitPoints = 40;
        var clumsy = Applied(stored.CampaignId, stored.Id, Gnibbo.Condition("clumsy", 2), value: 2);

        var levelled = Gnibbo.Build with { Level = 8 };
        stored.Apply(levelled);

        Assert.Equal(levelled, stored.ToBuild());
        Assert.Equal(40, stored.ToSession([clumsy]).CurrentHitPoints);

        var kept = Assert.Single(stored.ToSession([clumsy]).Effects);
        Assert.Equal(new EffectSource.Seeded("clumsy"), kept.Source);
        Assert.Equal(2, kept.Value);
    }

    [Fact]
    public void AStoredEffectRoundTripsThroughItsRowWhateverItsSource()
    {
        var custom = Gnibbo.Custom("Bless", ModifierType.Status, 1, On(StatKind.Will)) with
        {
            Duration = "1 minute",
        };

        var application = Applied(Guid.NewGuid(), Guid.NewGuid(), custom, custom.Value);
        var restored = application.AsActiveOn(application.Targets.Single());

        Assert.Equal(custom.Id, restored.Id);
        Assert.Equal(custom.Name, restored.Name);
        Assert.Equal(custom.Duration, restored.Duration);
        Assert.Equal(custom.Source.Kind, restored.Source.Kind);
        Assert.Equal(custom.Source.StoredKey, restored.Source.StoredKey);
        Assert.Equal(
            custom.Modifiers().Select(m => (m.Source, m.Type, m.Value)),
            restored.Modifiers().Select(m => (m.Source, m.Type, m.Value)));
    }

    // One application reaching several creatures is the whole point of the row, so the two
    // creatures here also prove that a sheet reads only what reached it.
    [Fact]
    public void OneApplicationReachingTwoCreaturesIsOneRowAndTwoSheets()
    {
        var bard = Guid.NewGuid();
        var fighter = Guid.NewGuid();
        var campaign = Guid.NewGuid();

        var anthem = new EffectApplication { Id = Guid.NewGuid(), CampaignId = campaign, Name = "Rallying Anthem" };
        anthem.Modifiers.Add(new EffectModifier(ModifierType.Status, 1, [On(StatKind.ArmorClass)]));
        anthem.SourceKind = "Custom";
        anthem.Targets.Add(new EffectTarget
        {
            ApplicationId = anthem.Id, Kind = EffectTargetKind.Character, TargetId = bard,
        });
        anthem.Targets.Add(new EffectTarget
        {
            ApplicationId = anthem.Id, Kind = EffectTargetKind.Character, TargetId = fighter,
        });

        Assert.Equal(anthem.Id, Assert.Single(Effects.On(bard, [anthem])).Id);
        Assert.Equal(anthem.Id, Assert.Single(Effects.On(fighter, [anthem])).Id);
        Assert.Empty(Effects.On(Guid.NewGuid(), [anthem]));
    }

    static EffectApplication Applied(Guid campaignId, Guid targetId, ActiveEffect effect, int value)
    {
        var application = new EffectApplication { Id = effect.Id, CampaignId = campaignId };
        application.Overwrite(effect, DurationTiming.None, null);
        application.Targets.Add(new EffectTarget
        {
            ApplicationId = effect.Id,
            Kind = EffectTargetKind.Character,
            TargetId = targetId,
            Value = value,
        });
        return application;
    }
}
