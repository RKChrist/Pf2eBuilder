using System.Collections.Immutable;

namespace Pf2e.Domain.Tracking;

/// <summary>
/// The five steps of a camping session, in the order the rules give them. It is a sequence and
/// not a set, which is why it is an enum with an order and the page draws it as a line.
/// </summary>
public enum CampStep
{
    PrepareCampsite,
    CampingActivities,
    Eating,
    Resting,
    DailyPreparations,
}

/// <summary>The four degrees of success, for the checks a camping session turns on.</summary>
public enum CampOutcome
{
    CriticalSuccess,
    Success,
    Failure,
    CriticalFailure,
}

public enum CampEntryKind
{
    /// <summary>A special meal the party knows how to cook. The ruleset has the activity of
    /// cooking one and none of the meals, so a recipe is always written in by the table.</summary>
    Recipe,

    /// <summary>A Camping activity that is in no book.</summary>
    Activity,
}

public enum MealKind
{
    Rations,
    BasicMeal,
    SpecialMeal,
}

/// <summary>
/// One thing the table wrote into its camp book. <paramref name="Dc"/> is a recipe's cooking DC,
/// which the rules say varies by recipe, and is null for an activity and for a recipe whose DC
/// nobody wrote down.
/// </summary>
public sealed record CampEntry(Guid Id, CampEntryKind Kind, string Name, string Does, int? Dc = null);

/// <summary>One Camping activity somebody took this session, and how it went.</summary>
public sealed record CampingTake(Guid CharacterId, string Activity, CampOutcome Outcome)
{
    public bool Succeeded => Outcome is CampOutcome.CriticalSuccess or CampOutcome.Success;
}

/// <summary>
/// What one character is eating. A special meal is either a recipe the table wrote into its camp
/// book, named by <paramref name="RecipeId"/>, or one of the ruleset's own, named by
/// <paramref name="RuleId"/>. One or the other, never both and never neither. Every other kind of
/// meal sets neither.
/// </summary>
public sealed record MealChoice(Guid CharacterId, MealKind Kind, Guid? RecipeId = null, string? RuleId = null);

/// <summary>
/// A camping session: where the party has got to, the two DCs of the place it is camping in, how
/// the campsite turned out, who has done what, who is eating what, and the camp book.
/// <para>One immutable value on the campaign, replaced whole, so an undo puts all of it back by
/// keeping a reference and the store writes it as one column.</para>
/// <para>The rules it enforces are the ones that are easy to lose track of with five people
/// talking: a Camping activity takes two hours, nobody takes more than four, and once anybody has
/// succeeded at one nobody may try it again until the next session.</para>
/// </summary>
public sealed record CampSite(
    CampStep Step,
    int ZoneDc,
    int EncounterDc,
    CampOutcome? Campsite,
    ImmutableArray<CampingTake> Taken,
    ImmutableArray<MealChoice> Meals,
    ImmutableArray<CampEntry> Book,
    int BasicIngredients = 0,
    int SpecialIngredients = 0,
    string? ZoneName = null)
{
    /// <summary>Level 1 and a quiet road: the DC by level for first level, and the commonest
    /// Encounter DC in the zones table. A DM camping anywhere in particular sets their own.</summary>
    public static CampSite Fresh { get; } = new(CampStep.PrepareCampsite, 15, 12, null, [], [], []);

    public const int ActivityMinutes = 120;
    public const int MostActivitiesEach = 4;
    public const int DailyPreparationsMinutes = 30;
    public const int MostEntries = 60;

    /// <summary>What the rules charge for a basic meal, per serving.</summary>
    public const int BasicMealIngredients = 2;

    // A default ImmutableArray is not an empty one and throws on enumeration, and this type is
    // reached by deserialization, where an absent list arrives as exactly that.
    public ImmutableArray<CampingTake> Taken { get; init; } = Taken.IsDefault ? [] : Taken;

    public ImmutableArray<MealChoice> Meals { get; init; } = Meals.IsDefault ? [] : Meals;

    public ImmutableArray<CampEntry> Book { get; init; } = Book.IsDefault ? [] : Book;

    /// <summary>A critically failed campsite is good enough to sleep in and for nothing else.</summary>
    public bool ActivitiesAllowed => Campsite is not CampOutcome.CriticalFailure;

    /// <summary>A poor campsite costs every Camping check two.</summary>
    public int ActivityPenalty => Campsite is CampOutcome.Failure ? -2 : 0;

    /// <summary>A perfect campsite is two harder to stumble on.</summary>
    public int EncounterDcTonight => EncounterDc + (Campsite is CampOutcome.CriticalSuccess ? 2 : 0);

    // Derived from Meals rather than subtracted from the larder when a meal is chosen, so changing
    // or clearing a meal puts the ingredients back by arithmetic and there is no second place for
    // the count to go wrong.
    //
    // Only the basic count. The rules state the price of a basic meal plainly and state no special
    // meal's cost at all, because the twenty-seven the ruleset carries keep theirs in prose the
    // licence withholds. Deducting for a recipe a table typed in while not deducting for an
    // official one would be arithmetic that is right half the time, which is worse than none, so
    // the special ingredient count stays the plain tally the table keeps by hand.
    public int BasicIngredientsSpent =>
        Meals.Count(meal => meal.Kind is MealKind.BasicMeal) * BasicMealIngredients;

    public int BasicIngredientsLeft => BasicIngredients - BasicIngredientsSpent;

    /// <summary>The recipe somebody is eating, or null when they are not eating one.</summary>
    public CampEntry? RecipeFor(MealChoice meal) =>
        meal.RecipeId is { } id
            ? Book.FirstOrDefault(entry => entry.Id == id && entry.Kind is CampEntryKind.Recipe)
            : null;

    public int TakenBy(Guid characterId) => Taken.Count(take => take.CharacterId == characterId);

    /// <summary>Closed for the session once anybody has succeeded at it. Cook Special Meal is the
    /// exception the rules name: it may be tried again, with a different recipe each time.</summary>
    public bool IsClosed(string activity) =>
        !IsCookSpecialMeal(activity)
        && Taken.Any(take => take.Succeeded && Same(take.Activity, activity));

    /// <summary>Why this character may not take this activity now, or null if they may.</summary>
    public string? Refusal(Guid characterId, string characterName, string activity)
    {
        if (!ActivitiesAllowed)
        {
            return "The campsite is a mess, which is good enough to sleep in and not for Camping activities.";
        }

        if (TakenBy(characterId) >= MostActivitiesEach)
        {
            return $"{characterName} has taken {MostActivitiesEach} Camping activities, which is as many as a day allows.";
        }

        return IsClosed(activity)
            ? $"Somebody has already succeeded at {activity} tonight, so it waits for the next camp."
            : null;
    }

    public CampSite With(CampingTake take) => this with { Taken = [.. Taken, take] };

    public CampSite With(CampEntry entry) =>
        this with { Book = [.. Book.Where(existing => existing.Id != entry.Id), entry] };

    /// <summary>A recipe somebody is eating stops being their meal when it leaves the book.</summary>
    public CampSite Without(Guid entryId) => this with
    {
        Book = [.. Book.Where(existing => existing.Id != entryId)],
        Meals = [.. Meals.Where(meal => meal.RecipeId != entryId)],
    };

    public CampSite With(MealChoice meal) =>
        this with { Meals = [.. Meals.Where(existing => existing.CharacterId != meal.CharacterId), meal] };

    public CampSite WithoutMealFor(Guid characterId) =>
        this with { Meals = [.. Meals.Where(meal => meal.CharacterId != characterId)] };

    /// <summary>Why the larder cannot serve this meal, or null if it can.</summary>
    public string? MealRefusal(MealChoice meal)
    {
        // Over the state the choice would produce, so swapping a meal frees what the old one held.
        var after = With(meal);

        return after.BasicIngredientsLeft < 0
            ? $"A basic meal takes {BasicMealIngredients} basic ingredients a serving. "
              + $"Tonight's meals would take {after.BasicIngredientsSpent} from a larder of {BasicIngredients}."
            : null;
    }

    /// <summary>The next camp. The zone, the book and the larder carry over; what happened at
    /// this one does not.</summary>
    public CampSite BrokenCamp() => this with
    {
        Step = CampStep.PrepareCampsite,
        Campsite = null,
        Taken = [],
        Meals = [],

        // Daily preparations are the rules' boundary, and past it the night's meals have been
        // eaten, so the larder settles to what is left rather than refunding a night that happened.
        BasicIngredients = BasicIngredientsLeft,
    };

    static bool IsCookSpecialMeal(string activity) => Same(activity, "Cook Special Meal");

    static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// How long a group has to set aside so that everybody gets eight hours and somebody is always
/// awake, with watches of equal length: eight hours shared among everyone but the one on watch.
/// <para>The rules print this as a table for two to nine. It is one formula, and the table is
/// that formula rounded to the minute, so the formula is what is kept.</para>
/// </summary>
public static class Watches
{
    public const int SleepMinutes = 8 * 60;

    /// <summary>Nine or more all keep hour watches over nine hours.</summary>
    const int Largest = 9;

    public static (int TotalMinutes, int EachWatchMinutes) For(int groupSize)
    {
        // One person cannot keep a watch and sleep, and the rules' table starts at two.
        if (groupSize < 2)
        {
            return (SleepMinutes, 0);
        }

        var sharing = Math.Min(groupSize, Largest);
        var total = (int)Math.Round(SleepMinutes * sharing / (double)(sharing - 1));
        return (total, (int)Math.Round(total / (double)sharing));
    }
}
