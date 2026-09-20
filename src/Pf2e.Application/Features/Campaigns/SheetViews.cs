using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// The one crossing between the domain and the wire, used by all four tracker handlers, so a
/// character that arrives through an import and one that arrives through a hit-point change are
/// the same shape on the screen.
/// </summary>
internal static class SheetViews
{
    public static CharacterSheetView Of(
        TrackedCharacter character,
        IEnumerable<EffectApplication> effects,
        int elapsedMinutes = 0)
    {
        var sheet = CharacterSheet.Compute(character.ToBuild(), character.ToSession(effects));

        return new CharacterSheetView(
            character.Id,
            character.Name,
            character.Level,
            character.ClassName,
            character.AncestryName,
            Of(sheet.ArmorClass),
            Of(sheet.Fortitude),
            Of(sheet.Reflex),
            Of(sheet.Will),
            Of(sheet.Perception),
            Of(sheet.ClassDc),
            sheet.CurrentHitPoints,
            sheet.MaxHitPoints,
            sheet.TemporaryHitPoints,
            sheet.HeroPoints,
            [.. sheet.Effects.Select(Of)],
            [.. sheet.Skills.Select(Of)],
            [.. sheet.Attacks.Select(Of)],
            sheet.SpellAttack is { } attack ? Of(attack) : null,
            sheet.SpellDc is { } dc ? Of(dc) : null,
            character.Spellcasting?.Tradition,
            Edit(character.ToBuild()),
            character.ExplorationActivity,
            ImmuneFor(character, elapsedMinutes),
            character.DowntimeActivity,
            character.DowntimeTaskLevel,
            character.DowntimeTaskLevel is { } task ? LevelBasedDc.For(task) : null,
            [.. character.Feats.Select(Of)],
            [.. character.Spells.Select(Of)],
            Attempts(character.ToBuild(), sheet));
    }

    static NamedBreakdownView Of(NamedBreakdown named) => new(named.Name, Of(named.Value), named.Rank?.ToString());

    /// <summary>
    /// What this character can attempt, decided here rather than on a screen, because a screen
    /// that decided it would be a second copy of the rules and the one that goes stale.
    /// <para>Perception is not a skill and Seek is rolled with it, so a name the skill list does
    /// not hold falls through to the sheet's own Perception rather than to zero.</para>
    /// </summary>
    static IReadOnlyList<AttemptView> Attempts(Character build, Sheet sheet)
    {
        ProficiencyRank RankOf(string skill) =>
            string.Equals(skill, "Perception", StringComparison.OrdinalIgnoreCase)
                ? build.Perception
                : build.Skills.FirstOrDefault(entry => entry.Name == skill).Rank;

        int TotalOf(string skill) =>
            string.Equals(skill, "Perception", StringComparison.OrdinalIgnoreCase)
                ? sheet.Perception.Total
                : sheet.Skills.FirstOrDefault(entry => entry.Name == skill)?.Value.Total ?? 0;

        return
        [
            .. SkillActions.All.Select(action =>
            {
                var skill = action.Best(RankOf, TotalOf);
                return new AttemptView(
                    action.Key,
                    action.Name,
                    skill,
                    TotalOf(skill),
                    action.MetBy(RankOf),
                    action.Required.ToString(),
                    action.When);
            }),
        ];
    }

    static SheetEntryView Of(SheetEntry entry) =>
        new(entry.Name, entry.Kind, entry.Level, entry.RuleId);

    /// <summary>Minutes left on the Treat Wounds immunity, subtracted here so no screen has to.
    /// Zero for somebody who has never been Treated, which is the same answer as somebody whose
    /// hour is up and is the answer both of them want.</summary>
    static int ImmuneFor(TrackedCharacter character, int elapsedMinutes) =>
        character.TreatedAtMinute is { } treated
            ? Math.Max(0, CampActivities.TreatWoundsImmunityMinutes - (elapsedMinutes - treated))
            : 0;

    /// <summary>The build as the edit screen holds it. Ranks and the key attribute travel by
    /// name, because a number would make the wire depend on the order of an enum.</summary>
    static CharacterBuildEdit Edit(Character build) => new(
        build.Name,
        build.Level,
        build.ClassName,
        build.AncestryName,
        build.KeyAttribute.ToString(),
        build.Attributes.Strength,
        build.Attributes.Dexterity,
        build.Attributes.Constitution,
        build.Attributes.Intelligence,
        build.Attributes.Wisdom,
        build.Attributes.Charisma,
        build.Fortitude.ToString(),
        build.Reflex.ToString(),
        build.Will.ToString(),
        build.Perception.ToString(),
        build.ClassDc.ToString(),
        build.ArmorRank.ToString(),
        build.ArmorName,
        build.ArmorItemBonus,
        build.ArmorDexCap,
        build.AncestryHitPoints,
        build.ClassHitPoints,
        build.BonusHitPoints,
        build.BonusHitPointsPerLevel,
        build.StatedMaxHitPoints);

    public static BreakdownSummary Of(Breakdown breakdown) => new(
        breakdown.Base,
        breakdown.Total,
        [.. breakdown.Applied.Select(Of)],
        [.. breakdown.Suppressed.Select(s => new SuppressedSummary(Of(s.Modifier), s.Reason))],
        breakdown.Bare);

    public static ActiveEffectView Of(ActiveEffect effect) => new(
        effect.Id,
        effect.Name,
        effect.Source.Kind,
        effect.Source.StoredKey,
        effect.Value,
        effect.Definition?.HasValue ?? false,
        effect.Duration,
        [.. effect.Source.StoredModifiers.Select(Of)]);

    /// <summary>The application id comes from the caller rather than from the spec, because the
    /// client names it and that is what makes applying the same effect twice idempotent.</summary>
    public static ActiveEffect ToActive(Guid id, EffectSpec spec) => new(
        id,
        spec.Name,
        spec.Value,
        EffectSource.From(spec.Kind, spec.Key, [.. spec.Modifiers.Select(ToModifier)]),
        spec.Duration);

    public static EffectApplicationView Of(EffectApplication application) => new(
        application.Id,
        application.Name,
        application.SourceKind,
        application.SourceKey,
        Domain.Effects.Find(application.SourceKey ?? string.Empty) is { HasValue: true }
            && application.SourceKind == "Seeded",
        application.Duration,
        application.Timing.ToString(),
        application.SourceCreatureId,
        application.PersistentDamage,
        application.PersistentDamageType,
        [.. application.Modifiers.Select(Of)],
        [.. application.Targets.Select(target => new EffectTargetView(
            target.Kind.ToString(), target.TargetId, target.Value, target.RemainingRounds))]);

    static ModifierSummary Of(Modifier modifier) => new(
        modifier.Source,
        modifier.Type.ToString(),
        modifier.Value,
        [.. modifier.Applies.Select(selector => selector.Describe())]);

    static EffectModifierView Of(EffectModifier modifier) => new(
        modifier.Type.ToString(),
        modifier.Value,
        [.. modifier.Applies.Select(Of)]);

    static SelectorSpecView Of(SelectorSpec spec) => new(
        spec.Kind.ToString(),
        spec.Kind is SelectorKind.Exactly ? spec.Stat.ToString() : null,
        spec.Kind is SelectorKind.Governed ? spec.Attribute.ToString() : null,
        spec.SkillName);

    // Parsing rather than trying is right here: the validator has already refused anything that
    // would not parse, so a failure at this point is a bug and not a bad request.
    static EffectModifier ToModifier(EffectModifierView view) => new(
        Enum.Parse<ModifierType>(view.Type, ignoreCase: true),
        view.Value,
        [.. view.Applies.Select(ToSpec)]);

    static SelectorSpec ToSpec(SelectorSpecView view) => new(
        Enum.Parse<SelectorKind>(view.Kind, ignoreCase: true),
        view.Stat is null ? default : Enum.Parse<StatKind>(view.Stat, ignoreCase: true),
        view.Attribute is null ? default : Enum.Parse<AttributeKind>(view.Attribute, ignoreCase: true),
        view.SkillName);
}
