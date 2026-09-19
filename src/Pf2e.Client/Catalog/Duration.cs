namespace Pf2e.Client.Catalog;

/// <summary>
/// Minutes, as somebody at a table would say them.
/// <para>"480 minutes" is a number nobody has ever said out loud about a night's sleep, and the
/// camp panel exists to answer "how long have we been here?" in the words the question was
/// asked in.</para>
/// </summary>
public static class Duration
{
    public static string Spoken(int minutes)
    {
        if (minutes <= 0)
        {
            return "no time at all";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => Plural(rest, "minute"),
            (_, 0) => Plural(hours, "hour"),
            _ => $"{Plural(hours, "hour")} {Plural(rest, "minute")}",
        };
    }

    static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
