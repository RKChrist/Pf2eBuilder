namespace Pf2e.Domain;

/// <summary>
/// What a modifier applies to. Pathfinder writes these as categories rather than as lists, so
/// the model does too. "Clumsy is a status penalty to Dex-based statistics" is one selector,
/// and it keeps being right when a new Dex-based skill is printed.
/// </summary>
public abstract record Selector
{
    public abstract bool Matches(StatTarget target);

    /// <summary>Readable enough to put in a breakdown the player reads.</summary>
    public abstract string Describe();

    /// <summary>One named statistic. A skill selector with no name means every skill.</summary>
    public static Selector Exactly(StatKind kind, string? skillName = null) => new ExactSelector(kind, skillName);

    /// <summary>Every statistic governed by one attribute, including attack rolls and damage.</summary>
    public static Selector Governed(AttributeKind attribute) => new AttributeSelector(attribute);

    /// <summary>The wording "all your checks and DCs", used by frightened and sickened.</summary>
    public static Selector AllChecksAndDcs { get; } = new AllChecksAndDcsSelector();

    public static Selector SavingThrows { get; } = new SavingThrowSelector();

    /// <summary>The wording "attack rolls", which covers a spell attack as much as a weapon
    /// one. A bless that missed the wizard would be the wrong bless.</summary>
    public static Selector AttackRolls { get; } = new AttackRollSelector();

    public static Selector Speeds { get; } = new SpeedSelector();

    static readonly Dictionary<StatKind, string> KindNames = new()
    {
        [StatKind.ArmorClass] = "Armor Class",
        [StatKind.Fortitude] = "Fortitude",
        [StatKind.Reflex] = "Reflex",
        [StatKind.Will] = "Will",
        [StatKind.Perception] = "Perception",
        [StatKind.Attack] = "weapon attacks",
        [StatKind.Damage] = "damage",
        [StatKind.Skill] = "Skill",
        [StatKind.ClassDc] = "Class DC",
        [StatKind.SpellAttack] = "Spell Attack",
        [StatKind.SpellDc] = "Spell DC",
        [StatKind.Speed] = "Speed",
    };

    sealed record ExactSelector(StatKind Kind, string? SkillName) : Selector
    {
        public override bool Matches(StatTarget target) =>
            Kind == target.Kind && (SkillName is null || SkillName.Equals(target.SkillName, StringComparison.OrdinalIgnoreCase));

        // A skill selector with no name is every skill, and "Skill" in a list that already reads
        // "attack rolls, Perception, saving throws" is the one word that does not say so.
        public override string Describe() =>
            SkillName ?? (Kind is StatKind.Skill ? "skills" : KindNames[Kind]);
    }

    sealed record AttributeSelector(AttributeKind Attribute) : Selector
    {
        public override bool Matches(StatTarget target) => target.GovernedBy == Attribute;

        public override string Describe() => $"{Attribute}-based";
    }

    sealed record AllChecksAndDcsSelector : Selector
    {
        public override bool Matches(StatTarget target) => target.IsCheck || target.IsDc;

        public override string Describe() => "all checks and DCs";
    }

    sealed record AttackRollSelector : Selector
    {
        public override bool Matches(StatTarget target) =>
            target.Kind is StatKind.Attack or StatKind.SpellAttack;

        public override string Describe() => "attack rolls";
    }

    sealed record SavingThrowSelector : Selector
    {
        public override bool Matches(StatTarget target) => target.IsSavingThrow;

        public override string Describe() => "saving throws";
    }

    sealed record SpeedSelector : Selector
    {
        public override bool Matches(StatTarget target) => target.Kind is StatKind.Speed;

        public override string Describe() => "speed";
    }
}
