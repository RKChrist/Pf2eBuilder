namespace Pf2e.Domain;

public static class Ability
{
    // C# integer division truncates toward zero, so (7 - 10) / 2 would give -1 instead of -2.
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);
}
