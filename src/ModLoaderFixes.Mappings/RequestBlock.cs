namespace ModLoaderFixes.Mappings;

/// <summary>The classes that the patches name by one kind of names, in the releases from one to another.</summary>
/// <param name="From">The first release.</param>
/// <param name="To">The last release.</param>
/// <param name="Names">The kind of names the classes and methods are given in.</param>
/// <param name="Classes">The classes to find in each of the releases.</param>
internal sealed record RequestBlock(string From, string To, Namespace Names, IReadOnlyList<ClassRequest> Classes)
{
    /// <summary>
    /// Reads <c>Mappings.txt</c>. A line <c>1.7.2 to 1.7.10 in srg</c> starts a block of releases and the kind of names, <c>srg</c> or <c>official</c>; each line
    /// after it names a property and a class, and each indented line under that a method and its descriptor. Lines that start with <c>#</c> are comments.
    /// </summary>
    /// <param name="text">The file's contents.</param>
    /// <returns>The blocks, in the file's order.</returns>
    internal static IReadOnlyList<RequestBlock> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<RequestBlock> blocks = [];
        List<ClassRequest>? classes = null;
        List<MethodName>? methods = null;
        int number = 0;

        foreach (string raw in text.Split(separator: '\n'))
        {
            number++;

            string line = raw.TrimEnd();
            string[] parts = line.Split(separator: ' ', StringSplitOptions.RemoveEmptyEntries);
            bool isIndented = line.StartsWith(value: ' ');

            if (parts.Length == 0 || parts[0].StartsWith(value: '#'))
                continue;

            if (parts is [{ } from, "to", { } to, "in", { } names])
            {
                classes = [];
                blocks.Add(new RequestBlock(from, to, ParseNames(names, number), classes));
            }
            else if (isIndented && methods is not null && parts is [{ } method, { } descriptor])
            {
                methods.Add(new MethodName(method, descriptor));
            }
            else if (!isIndented && classes is not null && parts is [{ } property, { } name])
            {
                methods = [];
                classes.Add(new ClassRequest(property, name, methods));
            }
            else
            {
                throw new FormatException($"Line {number} of the requests is neither a block, a class nor a method: {line}");
            }
        }

        return blocks;
    }

    private static Namespace ParseNames(string names, int number)
    {
        return names switch
        {
            "srg" => Namespace.Srg,
            "official" => Namespace.Official,
            _ => throw new FormatException($"Line {number} of the requests names {names}, which isn't srg or official."),
        };
    }
}
