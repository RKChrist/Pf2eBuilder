namespace Pf2e.Client.Catalog;

/// <summary>The four degrees of success, worst first, so that one step better is one more.</summary>
public enum Degree
{
    CriticalFailure,
    Failure,
    Success,
    CriticalSuccess,
}

/// <summary>
/// What each face of a d20 gets somebody, for one modifier against one DC.
/// <para>A DC table answers "what is the number". What a player asks is "what do I need on the
/// die", and that is twenty faces in four colours. The rule is the core one: beat the DC by ten
/// for a critical success, miss it by ten for a critical failure, and a natural 20 or a natural
/// 1 moves the result one step, whatever the total was.</para>
/// </summary>
public static class DieFaces
{
    public static Degree Of(int face, int modifier, int dc)
    {
        var total = face + modifier;
        var degree =
            total >= dc + 10 ? Degree.CriticalSuccess :
            total >= dc ? Degree.Success :
            total <= dc - 10 ? Degree.CriticalFailure :
            Degree.Failure;

        return face switch
        {
            20 => (Degree)Math.Min((int)degree + 1, (int)Degree.CriticalSuccess),
            1 => (Degree)Math.Max((int)degree - 1, (int)Degree.CriticalFailure),
            _ => degree,
        };
    }

    public static IReadOnlyList<(int Face, Degree Degree)> All(int modifier, int dc) =>
        [.. Enumerable.Range(1, 20).Select(face => (face, Of(face, modifier, dc)))];

    /// <summary>The lowest face that succeeds, and null when none does.</summary>
    public static int? Needs(int modifier, int dc) =>
        All(modifier, dc).Where(face => face.Degree >= Degree.Success).Select(face => (int?)face.Face).FirstOrDefault();
}
