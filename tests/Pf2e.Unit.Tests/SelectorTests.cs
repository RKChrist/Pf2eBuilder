using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class SelectorTests
{
    public static TheoryData<StatKind> EveryKind => [.. Enum.GetValues<StatKind>()];

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void EveryStatisticHasAPlayerReadableName(StatKind kind)
    {
        Assert.False(string.IsNullOrWhiteSpace(Selector.Exactly(kind).Describe()));
    }

    [Theory]
    [InlineData(StatKind.ArmorClass, "Armor Class")]
    [InlineData(StatKind.ClassDc, "Class DC")]
    [InlineData(StatKind.SpellDc, "Spell DC")]
    [InlineData(StatKind.SpellAttack, "Spell Attack")]
    public void CompoundStatisticsAreSpeltOutRatherThanLeftAsTheEnumToken(StatKind kind, string expected)
    {
        Assert.Equal(expected, Selector.Exactly(kind).Describe());
    }

    [Fact]
    public void ANamedSkillStillWinsOverTheKindName()
    {
        Assert.Equal("Stealth", Selector.Exactly(StatKind.Skill, "Stealth").Describe());
    }

    [Theory]
    [InlineData("Acrobatics", AttributeKind.Dexterity)]
    [InlineData("Athletics", AttributeKind.Strength)]
    [InlineData("Arcana", AttributeKind.Intelligence)]
    [InlineData("Medicine", AttributeKind.Wisdom)]
    [InlineData("Deception", AttributeKind.Charisma)]
    [InlineData("Warfare Lore", AttributeKind.Intelligence)]
    public void SkillsKnowTheirGoverningAttribute(string skill, AttributeKind expected)
    {
        Assert.Equal(expected, Skills.For(skill));
        Assert.Equal(expected, StatTarget.Skill(skill).GovernedBy);
    }

    [Fact]
    public void AnUnknownSkillHasNoGoverningAttribute()
    {
        Assert.Null(Skills.For("Basket Weaving"));
    }

    [Fact]
    public void AnAttributeSelectorMatchesEveryStatisticThatAttributeGoverns()
    {
        var dexBased = Selector.Governed(AttributeKind.Dexterity);

        Assert.True(dexBased.Matches(StatTarget.ArmorClass));
        Assert.True(dexBased.Matches(StatTarget.Reflex));
        Assert.True(dexBased.Matches(StatTarget.Skill("Stealth")));
        Assert.True(dexBased.Matches(StatTarget.Attack(AttributeKind.Dexterity)));
        Assert.False(dexBased.Matches(StatTarget.Will));
        Assert.False(dexBased.Matches(StatTarget.Speed));
    }

    [Fact]
    public void AnExactSkillSelectorMatchesOnlyThatSkill()
    {
        var stealthOnly = Selector.Exactly(StatKind.Skill, "Stealth");

        Assert.True(stealthOnly.Matches(StatTarget.Skill("Stealth")));
        Assert.True(stealthOnly.Matches(StatTarget.Skill("stealth")));
        Assert.False(stealthOnly.Matches(StatTarget.Skill("Acrobatics")));
    }

    [Fact]
    public void AnUnnamedSkillSelectorMatchesEverySkill()
    {
        var everySkill = Selector.Exactly(StatKind.Skill);

        Assert.True(everySkill.Matches(StatTarget.Skill("Stealth")));
        Assert.True(everySkill.Matches(StatTarget.Skill("Athletics")));
        Assert.False(everySkill.Matches(StatTarget.Perception));
    }

    [Fact]
    public void AllChecksAndDcsCoversChecksAndDcsButNotArmorClassOrSpeed()
    {
        var selector = Selector.AllChecksAndDcs;

        Assert.True(selector.Matches(StatTarget.Perception));
        Assert.True(selector.Matches(StatTarget.ClassDc(AttributeKind.Charisma)));
        Assert.False(selector.Matches(StatTarget.ArmorClass));
        Assert.False(selector.Matches(StatTarget.Speed));
        Assert.False(selector.Matches(StatTarget.Damage(AttributeKind.Strength)));
    }

    [Fact]
    public void SavingThrowsCoverExactlyTheThreeSaves()
    {
        var saves = Selector.SavingThrows;

        Assert.True(saves.Matches(StatTarget.Fortitude));
        Assert.True(saves.Matches(StatTarget.Reflex));
        Assert.True(saves.Matches(StatTarget.Will));
        Assert.False(saves.Matches(StatTarget.Perception));
    }
}
