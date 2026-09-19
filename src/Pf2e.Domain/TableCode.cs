namespace Pf2e.Domain;

/// <summary>
/// A table is a short code and anyone with the code is at that table. Generation lives in the
/// client: it makes a code up and the server accepts any valid one, which is why there is no
/// operation that creates a table.
/// </summary>
public static class TableCode
{
    public const int GeneratedLength = 6;

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsValid(string code)
    {
        var normalized = Normalize(code);
        return normalized.Length is >= 4 and <= 12
            && normalized.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
    }
}
