using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// One skill action and what a character needs to attempt it.
/// <para><see cref="Skills"/> is every skill it can be rolled with, which is usually one and is
/// four for Decipher Writing. <see cref="Required"/> is the rank needed in one of them, and
/// <see cref="ProficiencyRank.Untrained"/> means anybody may try.</para>
/// </summary>
public sealed record SkillAction(
    string Key,
    string Name,
    ImmutableArray<string> Skills,
    ProficiencyRank Required,
    string When,
    string SourceRef,
    bool Verified = false)
{
    /// <summary>The skill this character would actually roll: the best of the ones the action
    /// allows, by rank and then by the number itself.</summary>
    public string Best(Func<string, ProficiencyRank> rankOf, Func<string, int> totalOf) =>
        Skills
            .OrderByDescending(skill => (int)rankOf(skill))
            .ThenByDescending(totalOf)
            .First();

    /// <summary>Whether this character meets the requirement, in one of the skills it allows.</summary>
    public bool MetBy(Func<string, ProficiencyRank> rankOf) =>
        Required is ProficiencyRank.Untrained
        || Skills.Any(skill => rankOf(skill) >= Required);
}

/// <summary>
/// What a character can attempt, and what they cannot.
/// <para>Hand-written against the printed rules, like <see cref="Buffs"/> and for the same
/// reason: Archives of Nethys publishes an action's name and its traits and not its requirement.
/// Of the 38 actions carrying the Exploration trait, seven state a requirement at all and one of
/// those seven is a plain proficiency phrase. Decipher Writing states none — its "trained in
/// Arcana, Occultism, Religion, or Society" lives in the rule text this project does not import
/// by licence — and Decipher Writing is the design document's own worked example.</para>
/// <para>So every entry here carries <see cref="SkillAction.Verified"/> false and names the page
/// it came from. Check them before a session that matters; the page is in the record so checking
/// one takes a minute.</para>
/// </summary>
public static class SkillActions
{
    const string PlayerCore = "Player Core, Skills";

    static SkillAction Anyone(string key, string name, string when, params string[] skills) =>
        new(key, name, [.. skills], ProficiencyRank.Untrained, when, PlayerCore);

    static SkillAction Trained(string key, string name, string when, params string[] skills) =>
        new(key, name, [.. skills], ProficiencyRank.Trained, when, PlayerCore);

    /// <summary>
    /// The ones a table reaches for. Not every printed action: an action nobody has attempted at
    /// a table in a year would be a row everybody scrolls past, and the catalogue already lists
    /// all 551 of them.
    /// </summary>
    public static ImmutableArray<SkillAction> All { get; } =
    [
        // Anybody may try these, so the list answers "what do I roll" rather than "may I".
        Anyone("seek", "Seek", "Looks for something hidden or unnoticed.", "Perception"),
        Anyone("sense-motive", "Sense Motive", "Reads whether somebody is lying.", "Perception"),
        Anyone("recall-knowledge", "Recall Knowledge", "Remembers something about what you are looking at.",
            "Arcana", "Crafting", "Medicine", "Nature", "Occultism", "Religion", "Society"),
        Anyone("gather-information", "Gather Information", "Asks around town about somebody or something.", "Diplomacy"),
        Anyone("make-an-impression", "Make an Impression", "Spends a minute being liked.", "Diplomacy"),
        Anyone("request", "Request", "Asks somebody for something they could refuse.", "Diplomacy"),
        Anyone("lie", "Lie", "Says something untrue and is believed or is not.", "Deception"),
        Anyone("create-a-diversion", "Create a Diversion", "Draws every eye somewhere else for a moment.", "Deception"),
        Anyone("impersonate", "Impersonate", "Passes for somebody else.", "Deception"),
        Anyone("coerce", "Coerce", "Threatens somebody into cooperating, and they remember it.", "Intimidation"),
        Anyone("demoralize", "Demoralize", "One creature becomes frightened 1, or 2 on a critical success.", "Intimidation"),
        Anyone("hide", "Hide", "Becomes hidden from anybody who is not watching.", "Stealth"),
        Anyone("sneak", "Sneak", "Moves while staying unnoticed.", "Stealth"),
        Anyone("balance", "Balance", "Crosses something narrow or slippery.", "Acrobatics"),
        Anyone("tumble-through", "Tumble Through", "Moves through a foe's space.", "Acrobatics"),
        Anyone("climb", "Climb", "Climbs a wall or a rope, at a quarter speed on a success.", "Athletics"),
        Anyone("swim", "Swim", "Crosses water, at a quarter speed on a success.", "Athletics"),
        Anyone("force-open", "Force Open", "Breaks a door, a lid or a lock by being stronger than it.", "Athletics"),
        Anyone("grapple", "Grapple", "Holds a creature, leaving it grabbed.", "Athletics"),
        Anyone("shove", "Shove", "Pushes a creature back.", "Athletics"),
        Anyone("trip", "Trip", "Knocks a creature prone.", "Athletics"),
        Anyone("perform", "Perform", "Plays, sings or acts for somebody.", "Performance"),
        Anyone("subsist", "Subsist", "Finds food and shelter for the day.", "Survival", "Society"),
        Anyone("sense-direction", "Sense Direction", "Works out which way is which.", "Survival"),
        Anyone("palm-an-object", "Palm an Object", "Takes something small while somebody is watching.", "Thievery"),
        Anyone("steal", "Steal", "Takes something a creature is wearing or holding.", "Thievery"),

        // These need training, which is the half the design document's walkthrough turns on.
        Trained("decipher-writing", "Decipher Writing", "Reads something written to be hard to read.",
            "Arcana", "Occultism", "Religion", "Society"),
        Trained("identify-magic", "Identify Magic", "Works out what a magical thing does.",
            "Arcana", "Nature", "Occultism", "Religion"),
        Trained("learn-a-spell", "Learn a Spell", "Adds a spell to a repertoire or a spellbook.",
            "Arcana", "Nature", "Occultism", "Religion"),
        Trained("treat-wounds", "Treat Wounds", "Ten minutes and a healer's toolkit. Heals, then an hour of immunity.", "Medicine"),
        Trained("treat-disease", "Treat Disease", "A day spent tending somebody, for a bonus to their next save.", "Medicine"),
        Trained("administer-first-aid", "Administer First Aid", "Stops bleeding, or stabilises somebody who is dying.", "Medicine"),
        Trained("craft", "Craft", "Makes something over days, with materials up front.", "Crafting"),
        Trained("repair", "Repair", "Mends a broken thing, with a repair kit.", "Crafting"),
        Trained("track", "Track", "Follows what went this way.", "Survival"),
        Trained("cover-tracks", "Cover Tracks", "Makes what you left harder to follow.", "Survival"),
        Trained("create-forgery", "Create Forgery", "A day and a handwriting sample.", "Society"),
        Trained("disarm", "Disarm", "Knocks a weapon out of a creature's grip.", "Athletics"),
        Trained("high-jump", "High Jump", "Jumps higher than jumping.", "Athletics"),
        Trained("long-jump", "Long Jump", "Jumps further than jumping.", "Athletics"),
        Trained("feint", "Feint", "Makes a creature off-guard against your next attack.", "Deception"),
        Trained("conceal-an-object", "Conceal an Object", "Hides something on your person.", "Stealth"),
        Trained("squeeze", "Squeeze", "Gets through a gap smaller than you are.", "Acrobatics"),
        Trained("maneuver-in-flight", "Maneuver in Flight", "Does something difficult while flying.", "Acrobatics"),
        Trained("pick-a-lock", "Pick a Lock", "Opens a lock without its key, with thieves' tools.", "Thievery"),
        Trained("disable-a-device", "Disable a Device", "Turns off a trap or a mechanism.", "Thievery"),
    ];

    public static SkillAction? Find(string? key) =>
        key is null
            ? null
            : All.FirstOrDefault(action =>
                string.Equals(action.Key, key, StringComparison.OrdinalIgnoreCase));
}
