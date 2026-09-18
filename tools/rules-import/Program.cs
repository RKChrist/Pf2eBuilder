namespace Pf2e.Tools.RulesImport;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            return await Dispatch(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> Dispatch(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage("no verb given");
        }

        var verb = args[0];
        if (verb is not ("pull" or "transform"))
        {
            return Usage($"unknown verb '{verb}'");
        }

        var only = new List<string>();
        var force = false;
        string? index = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--only":
                    if (i + 1 >= args.Length)
                    {
                        return Usage("--only needs a category");
                    }

                    var category = args[++i];
                    if (!FieldPolicy.Categories.Contains(category))
                    {
                        return Usage(
                            $"unknown category '{category}'; valid categories: {string.Join(", ", FieldPolicy.Categories)}");
                    }

                    if (!only.Contains(category))
                    {
                        only.Add(category);
                    }

                    break;

                case "--force":
                    if (verb != "pull")
                    {
                        return Usage("--force is only valid on pull");
                    }

                    force = true;
                    break;

                case "--index":
                    if (verb != "transform")
                    {
                        return Usage("--index is only valid on transform");
                    }

                    if (i + 1 >= args.Length)
                    {
                        return Usage("--index needs a name");
                    }

                    index = args[++i];
                    break;

                default:
                    return Usage($"unknown flag '{args[i]}'");
            }
        }

        var categories = only.Count > 0
            ? FieldPolicy.Categories.Where(only.Contains).ToList()
            : (IReadOnlyList<string>)FieldPolicy.Categories;
        return verb == "pull"
            ? await Pull.RunAsync(categories, force)
            : Transform.Run(categories, index);
    }

    static int Usage(string problem)
    {
        Console.Error.WriteLine($"error: {problem}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  rules-import pull [--only <category>]... [--force]");
        Console.Error.WriteLine("  rules-import transform [--only <category>]... [--index <name>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine($"categories: {string.Join(", ", FieldPolicy.Categories)}");
        return 2;
    }
}
