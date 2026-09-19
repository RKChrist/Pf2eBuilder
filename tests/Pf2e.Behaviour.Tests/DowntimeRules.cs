using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// The level-based DCs, entry by entry.
/// <para>This is the oracle and the formula is the implementation, not the other way round. A
/// formula that drifted from the printed table would put a wrong DC on every downtime roll, and
/// nobody would catch it because the app would be confidently consistent. So the table is typed
/// out here and the code is checked against it.</para>
/// </summary>
public class DowntimeRules
{
    public static TheoryData<int, int> PrintedTable => new()
    {
        { 0, 14 }, { 1, 15 }, { 2, 16 }, { 3, 18 }, { 4, 19 },
        { 5, 20 }, { 6, 22 }, { 7, 23 }, { 8, 24 }, { 9, 26 },
        { 10, 27 }, { 11, 28 }, { 12, 30 }, { 13, 31 }, { 14, 32 },
        { 15, 34 }, { 16, 35 }, { 17, 36 }, { 18, 38 }, { 19, 39 },
        { 20, 40 }, { 21, 42 }, { 22, 44 }, { 23, 46 }, { 24, 48 },
        { 25, 50 },
    };

    [Theory]
    [MemberData(nameof(PrintedTable))]
    public void TheLevelBasedDcMatchesThePrintedTable(int level, int dc)
    {
        Assert.Equal(dc, LevelBasedDc.For(level));
    }

    [Fact]
    public void AndIsClampedRatherThanExtrapolated()
    {
        // The table stops at 25. A task level of 40 is somebody mistyping, and answering 70 with
        // a straight face would be worse than answering the highest number the book prints.
        Assert.Equal(LevelBasedDc.For(0), LevelBasedDc.For(-3));
        Assert.Equal(LevelBasedDc.For(25), LevelBasedDc.For(40));
    }

    [Fact]
    public void EveryDowntimeActivityNamesItselfAndSaysWhatADayOfItIs()
    {
        Assert.Equal(7, DowntimeActivities.All.Count);
        Assert.All(DowntimeActivities.All, activity =>
        {
            Assert.False(string.IsNullOrWhiteSpace(activity.Name));
            Assert.True(activity.What.Length > 40, activity.Name);
        });

        Assert.NotNull(DowntimeActivities.Find("earn-income"));
        Assert.Null(DowntimeActivities.Find("day-drinking"));
    }

    [Fact]
    public void EarnIncomeSaysThePaymentTableIsInTheBookRatherThanInventingIt()
    {
        // The table is a currency per day for each task level, each proficiency rank and each
        // degree of success. This project imports no rule text, so the alternative to saying so
        // would be typing several hundred numbers out of a book from memory, and a wrong payment
        // every downtime day is worse than one row somebody reads themselves.
        Assert.Contains("table is in the book", DowntimeActivities.EarnIncome.What);
    }
}
