using Pf2e.Contracts.Rules;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;
using Pf2e.Domain.Tracking;

namespace Pf2e.Application.Features.Tracker;

/// <summary>
/// The one crossing between the domain and the wire, used by all four tracker handlers, so a
/// character that arrives through an import and one that arrives through a hit-point change are
/// the same shape on the screen.
/// </summary>
internal static class SheetViews
{
    public static CharacterSheetView Of(TrackedCharacter character)
    {
        var sheet = CharacterSheet.Compute(character.ToBuild(), character.ToSession());

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
            character.Spellcasting?.Tradition);
    }

    static NamedBreakdownView Of(NamedBreakdown named) => new(named.Name, Of(named.Value), named.Rank?.ToString());

    public static BreakdownSummary Of(Breakdown breakdown) => new(
        breakdown.Base,
        breakdown.Total,
        [.. breakdown.Applied.Select(Of)],
        [.. breakdown.Suppressed.Select(s => new SuppressedSummary(Of(s.Modifier), s.Reason))]);

    public static ActiveEffectView Of(ActiveEffect effect) => new(
        effect.Id,
        effect.Name,
        effect.Source.Kind,
        effect.Source.StoredKey,
        effect.Value,
        effect.Definition?.HasValue ?? false,
        effect.Duration,
        [.. effect.Source.StoredModifiers.Select(Of)]);

    /// <summary>The slot id comes from the caller rather than from the spec, because the client
    /// names the slot and that is what makes applying the same effect twice idempotent.</summary>
    public static ActiveEffect ToActive(Guid id, EffectSpec spec) => new(
        id,
        spec.Name,
        spec.Value,
        EffectSource.From(spec.Kind, spec.Key, [.. spec.Modifiers.Select(ToModifier)]),
        spec.Duration);

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
