using Pf2e.Components;

namespace Pf2e.Client.State;

/// <summary>
/// The levels the search accepts, which is also the whole scale the filter offers. A range that
/// spans it is no filter at all, so an end sitting on the scale's own end is sent as no bound
/// rather than as a number, and records without a level stay in the results.
/// </summary>
public static class LevelScale
{
    public const int Floor = -1;

    public const int Ceiling = 30;

    public static IntRange Whole { get; } = new(Floor, Ceiling);

    public static int? LowBound(IntRange levels) => levels.Low == Floor ? null : levels.Low;

    public static int? HighBound(IntRange levels) => levels.High == Ceiling ? null : levels.High;
}
