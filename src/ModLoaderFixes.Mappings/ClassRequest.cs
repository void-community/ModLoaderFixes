using System.Globalization;

namespace ModLoaderFixes.Mappings;

/// <summary>A class that a patch names, with the releases to find it in and the methods the patch names, as a <c>MappedClass</c> item of the agent's project lists it.</summary>
/// <param name="Property">The property of <c>MappedClasses</c> that holds the class's names.</param>
/// <param name="Name">The class's name.</param>
/// <param name="From">The first release to find it in.</param>
/// <param name="To">The last release to find it in.</param>
/// <param name="Methods">The methods of the class that patches name, whose names each release gets in the same order.</param>
internal sealed record ClassRequest(string Property, string Name, string From, string To, IReadOnlyList<MethodName> Methods)
{
    /// <summary>Gets the kind of names the class and its methods are given in: those Forge runs its releases under.</summary>
    internal Namespace Names => NamesOf(From);

    /// <summary>Gets the names Forge runs a release under: MCP's up to Minecraft 1.16.5, and Mojang's from 1.17, which NeoForge and the unobfuscated releases share.</summary>
    /// <param name="version">The release.</param>
    /// <returns>The kind of names.</returns>
    internal static Namespace NamesOf(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        string[] parts = version.Split(separator: '.');
        bool isMcp = parts is ["1", { } minor, ..] && int.TryParse(minor, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number <= 16;

        return isMcp ? Namespace.Srg : Namespace.Official;
    }

    /// <summary>
    /// Reads the requests that the agent's build writes from its <c>MappedClass</c> items, one per line: the property, the class, the first and last release, and the
    /// methods, each its name followed by its descriptor and separated by spaces, all separated by tabs.
    /// </summary>
    /// <param name="text">The file's contents.</param>
    /// <returns>The requests, in the order of the items.</returns>
    internal static IReadOnlyList<ClassRequest> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<ClassRequest> requests = [];
        int number = 0;

        foreach (string raw in text.Split(separator: '\n'))
        {
            number++;

            string line = raw.TrimEnd(trimChar: '\r');

            if (line.Length == 0)
                continue;

            if (line.Split(separator: '\t') is not [{ } property, { } name, { } from, { } to, { } methods])
                throw new FormatException($"Request {number} has fewer or more than five fields: {line}");

            MethodName[] parsed = [.. methods.Split(separator: ' ', StringSplitOptions.RemoveEmptyEntries).Select(method => ParseMethod(method, number))];

            requests.Add(new ClassRequest(property, name, from, to, parsed));
        }

        return requests;
    }

    private static MethodName ParseMethod(string method, int number)
    {
        int descriptor = method.IndexOf(value: '(', StringComparison.Ordinal);

        return descriptor <= 0
            ? throw new FormatException($"Request {number} names a method without its descriptor: {method}")
            : new MethodName(method[..descriptor], method[descriptor..]);
    }
}
