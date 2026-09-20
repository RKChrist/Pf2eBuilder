using FluentValidation;
using MediatR;
using Pf2e.Application.Abstractions;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Application.Features.Campaigns;

/// <summary>
/// Import is how a character arrives. It is not how a character stays correct: a level-up
/// between sessions, a number Pathbuilder got wrong and a homebrew ancestry all need an edit.
/// <para>This writes the build layer through <see cref="Domain.Tracking.TrackedCharacter.Apply"/>,
/// which is the same and only path a re-import takes. That is what makes "the session layer
/// survives an edit exactly as it survives a re-import" true by construction rather than by two
/// implementations agreeing.</para>
/// </summary>
public sealed record EditCharacter(string Code, Guid CharacterId, CharacterBuildEdit Build)
    : IRequest<CharacterSheetView>;

public sealed class EditCharacterValidator : AbstractValidator<EditCharacter>
{
    public EditCharacterValidator()
    {
        RuleFor(c => c.Code).Must(CampaignCode.IsValid)
                            .WithMessage("A campaign code is four to twelve letters and digits.");
        RuleFor(c => c.CharacterId).NotEmpty();
        RuleFor(c => c.Build).NotNull().SetValidator(new CharacterBuildEditValidator()!);
    }
}

public sealed class CharacterBuildEditValidator : AbstractValidator<CharacterBuildEdit>
{
    public CharacterBuildEditValidator()
    {
        RuleFor(b => b.Name).NotEmpty().MaximumLength(128);
        RuleFor(b => b.Level).InclusiveBetween(1, 20);
        RuleFor(b => b.ClassName).NotEmpty().MaximumLength(128);
        RuleFor(b => b.AncestryName).NotEmpty().MaximumLength(128);
        RuleFor(b => b.ArmorName).NotEmpty().MaximumLength(128);
        RuleFor(b => b.KeyAttribute).Must(Named<AttributeKind>)
                                    .WithMessage("Key attribute is one of the six attributes.");

        foreach (var attribute in new[]
        {
            (Name: nameof(CharacterBuildEdit.Strength), Of: (Func<CharacterBuildEdit, int>)(b => b.Strength)),
            (nameof(CharacterBuildEdit.Dexterity), b => b.Dexterity),
            (nameof(CharacterBuildEdit.Constitution), b => b.Constitution),
            (nameof(CharacterBuildEdit.Intelligence), b => b.Intelligence),
            (nameof(CharacterBuildEdit.Wisdom), b => b.Wisdom),
            (nameof(CharacterBuildEdit.Charisma), b => b.Charisma),
        })
        {
            RuleFor(b => attribute.Of(b)).InclusiveBetween(-5, 10)
                                         .OverridePropertyName(attribute.Name);
        }

        foreach (var rank in new[]
        {
            (Name: nameof(CharacterBuildEdit.Fortitude), Of: (Func<CharacterBuildEdit, string>)(b => b.Fortitude)),
            (nameof(CharacterBuildEdit.Reflex), b => b.Reflex),
            (nameof(CharacterBuildEdit.Will), b => b.Will),
            (nameof(CharacterBuildEdit.Perception), b => b.Perception),
            (nameof(CharacterBuildEdit.ClassDc), b => b.ClassDc),
            (nameof(CharacterBuildEdit.ArmorRank), b => b.ArmorRank),
        })
        {
            RuleFor(b => rank.Of(b)).Must(Named<ProficiencyRank>)
                                    .WithMessage("A proficiency rank is Untrained, Trained, Expert, Master or Legendary.")
                                    .OverridePropertyName(rank.Name);
        }

        RuleFor(b => b.ArmorItemBonus).InclusiveBetween(0, 10);
        RuleFor(b => b.ArmorDexCap).InclusiveBetween(0, 10).When(b => b.ArmorDexCap is not null);
        RuleFor(b => b.AncestryHitPoints).InclusiveBetween(0, 20);
        RuleFor(b => b.ClassHitPoints).InclusiveBetween(0, 20);
        RuleFor(b => b.BonusHitPoints).InclusiveBetween(0, 200);
        RuleFor(b => b.BonusHitPointsPerLevel).InclusiveBetween(0, 20);
    }

    static bool Named<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out _);
}

public sealed class EditCharacterHandler(ITrackerDbContext db, ICampaignBroadcaster broadcaster)
    : IRequestHandler<EditCharacter, CharacterSheetView>
{
    public async Task<CharacterSheetView> Handle(EditCharacter command, CancellationToken ct)
    {
        var (campaign, _) = await CampaignAccess.LoadAsync(db, command.Code, null, ct);

        if (campaign.Characters.FirstOrDefault(c => c.Id == command.CharacterId) is not { } character)
        {
            throw new CombatantNotFoundException("No character in this campaign has that id.");
        }

        // ApplyEdits, not Apply: this command carries the build layer and nothing else, and
        // writing the rest from it would erase the weapons, skills, feats and spells the import
        // brought in.
        character.ApplyEdits(Build(command.Build));
        await db.SaveChangesAsync(ct);

        var sheet = SheetViews.Of(character, campaign.EffectApplications);
        await broadcaster.CharacterChangedAsync(campaign.Code, sheet, ct);
        return sheet;
    }

    // Parsing rather than trying: the validator has already refused anything that would not
    // parse, so a failure here is a bug and not a bad request.
    static Character Build(CharacterBuildEdit edit) => new(
        edit.Name,
        edit.Level,
        edit.ClassName,
        edit.AncestryName,
        Enum.Parse<AttributeKind>(edit.KeyAttribute, ignoreCase: true),
        new AttributeModifiers(
            edit.Strength, edit.Dexterity, edit.Constitution,
            edit.Intelligence, edit.Wisdom, edit.Charisma),
        Enum.Parse<ProficiencyRank>(edit.Fortitude, ignoreCase: true),
        Enum.Parse<ProficiencyRank>(edit.Reflex, ignoreCase: true),
        Enum.Parse<ProficiencyRank>(edit.Will, ignoreCase: true),
        Enum.Parse<ProficiencyRank>(edit.Perception, ignoreCase: true),
        Enum.Parse<ProficiencyRank>(edit.ClassDc, ignoreCase: true),
        Enum.Parse<ProficiencyRank>(edit.ArmorRank, ignoreCase: true),
        edit.ArmorName,
        edit.ArmorItemBonus,
        edit.ArmorDexCap,
        edit.AncestryHitPoints,
        edit.ClassHitPoints,
        edit.BonusHitPoints,
        edit.BonusHitPointsPerLevel);
}
