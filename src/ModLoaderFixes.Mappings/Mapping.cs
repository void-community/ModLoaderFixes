using System.Text.RegularExpressions;

namespace ModLoaderFixes.Mappings;

/// <summary>The names that one source gives the obfuscated classes and methods of one Minecraft version, and the other way round.</summary>
internal sealed partial class Mapping
{
    private readonly Dictionary<string, string> _namedClasses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _obfuscatedClasses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _namedMethods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MethodName> _obfuscatedMethods = new(StringComparer.Ordinal);
    private readonly List<PendingMethod> _pending = [];

    private Mapping(string source, bool isIdentity)
    {
        Source = source;
        IsIdentity = isIdentity;
    }

    /// <summary>Gets whether the mapping is of an unobfuscated release, which keeps every name.</summary>
    internal bool IsIdentity { get; }

    /// <summary>Gets what the mapping is, for routes and errors, for example <c>Forge's 1.7.10</c>.</summary>
    internal string Source { get; }

    /// <summary>A version that Mojang ships without obfuscation, whose names are its official ones.</summary>
    /// <param name="source">What the mapping is.</param>
    /// <returns>The mapping.</returns>
    internal static Mapping Identity(string source)
    {
        return new Mapping(source, isIdentity: true);
    }

    /// <summary>Replaces each class in a method descriptor with what a map returns for it, or keeps it when the map returns null.</summary>
    /// <param name="descriptor">The types of a method's parameters and result.</param>
    /// <param name="map">The map of class names.</param>
    /// <returns>The descriptor with the mapped classes.</returns>
    internal static string MapDescriptor(string descriptor, Func<string, string?> map)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(map);

        return DescriptorClass().Replace(descriptor, match => "L" + (map(Group(match, name: "name")) ?? Group(match, name: "name")) + ";");
    }

    /// <summary>Reads Mojang's ProGuard files, which name the obfuscated classes and methods of Minecraft 1.14.4 to 1.21.11, the client's and then the server's.</summary>
    /// <param name="source">What the mapping is.</param>
    /// <param name="texts">The files.</param>
    /// <returns>The mapping.</returns>
    internal static Mapping ReadProGuard(string source, params IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        Mapping mapping = new(source, isIdentity: false);

        foreach (string text in texts)
        {
            string? owner = null;

            foreach (string line in Lines(text))
            {
                bool isMember = line.StartsWith(value: ' ');
                Match type = ProGuardClass().Match(line);
                Match method = ProGuardMethod().Match(line);

                if (!isMember && type.Success)
                {
                    owner = Group(type, name: "obfuscated").Replace(oldChar: '.', newChar: '/');
                    mapping.AddClass(owner, Group(type, name: "named").Replace(oldChar: '.', newChar: '/'));
                }
                else if (owner is not null && method.Success && !Group(method, name: "named").StartsWith(value: '<'))
                {
                    string[] parameters = Group(method, name: "parameters").Split(separator: ',', StringSplitOptions.RemoveEmptyEntries);
                    string descriptor = "(" + string.Concat(parameters.Select(JavaType)) + ")" + JavaType(Group(method, name: "returns"));

                    mapping.AddMethod(owner, Group(method, name: "obfuscated"), descriptor, Group(method, name: "named"), isNamedDescriptor: true);
                }
            }
        }

        return mapping.Complete();
    }

    /// <summary>Reads MCP's SRG file, <c>joined.srg</c>, which Forge publishes up to Minecraft 1.12.2.</summary>
    /// <param name="source">What the mapping is.</param>
    /// <param name="text">The file.</param>
    /// <returns>The mapping.</returns>
    internal static Mapping ReadSrg(string source, string text)
    {
        Mapping mapping = new(source, isIdentity: false);

        foreach (string line in Lines(text))
        {
            string[] parts = line.Split(separator: ' ');

            if (parts is ["CL:", { } obfuscated, { } named])
                mapping.AddClass(obfuscated, named);
            else if (parts is ["MD:", { } method, { } descriptor, { } namedMethod, _])
                mapping.AddMethod(Owner(method), SimpleName(method), descriptor, SimpleName(namedMethod), isNamedDescriptor: false);
        }

        return mapping.Complete();
    }

    /// <summary>Reads a Tiny v2 file from the official names to Ornithe's intermediary ones, as Calamus publishes for each version up to Minecraft 1.14.4.</summary>
    /// <param name="source">What the mapping is.</param>
    /// <param name="text">The file.</param>
    /// <returns>The mapping.</returns>
    internal static Mapping ReadTiny(string source, string text)
    {
        Mapping mapping = new(source, isIdentity: false);
        string? owner = null;
        string[] lines = [.. Lines(text)];

        if (lines is not [{ } header, ..] || !header.StartsWith(value: "tiny\t2\t0\tofficial\tintermediary", StringComparison.Ordinal))
            throw new InvalidDataException($"{source} isn't a Tiny v2 file from official to intermediary names.");

        foreach (string line in lines.Skip(count: 1))
        {
            string[] parts = line.Split(separator: '\t');

            if (parts is ["c", { } obfuscated, { } named])
            {
                owner = obfuscated;
                mapping.AddClass(obfuscated, named);
            }
            else if (owner is not null && parts is ["", "m", { } descriptor, { } method, { } namedMethod])
            {
                mapping.AddMethod(owner, method, descriptor, namedMethod, isNamedDescriptor: false);
            }
        }

        return mapping.Complete();
    }

    /// <summary>Reads MCPConfig's TSRG file, <c>config/joined.tsrg</c>, which Forge publishes for Minecraft 1.13 to 1.16.5.</summary>
    /// <param name="source">What the mapping is.</param>
    /// <param name="text">The file.</param>
    /// <returns>The mapping.</returns>
    internal static Mapping ReadTsrg(string source, string text)
    {
        Mapping mapping = new(source, isIdentity: false);
        string? owner = null;

        foreach (string line in Lines(text))
        {
            string[] parts = line.TrimStart(trimChar: '\t').Split(separator: ' ');

            if (!line.StartsWith(value: '\t') && parts is [{ } obfuscated, { } named])
            {
                owner = obfuscated;
                mapping.AddClass(obfuscated, named);
            }
            else if (owner is not null && parts is [{ } method, { } descriptor, { } namedMethod])
            {
                mapping.AddMethod(owner, method, descriptor, namedMethod, isNamedDescriptor: false);
            }
        }

        return mapping.Complete();
    }

    /// <summary>Gets the name of an obfuscated class, or null when the mapping doesn't have it.</summary>
    /// <param name="obfuscated">The obfuscated name.</param>
    /// <returns>The name.</returns>
    internal string? NamedClass(string obfuscated)
    {
        return IsIdentity ? obfuscated : _namedClasses.GetValueOrDefault(obfuscated);
    }

    /// <summary>Puts the names of their classes into a descriptor of obfuscated ones.</summary>
    /// <param name="descriptor">The obfuscated descriptor.</param>
    /// <returns>The descriptor by the names of its classes.</returns>
    internal string NamedDescriptor(string descriptor)
    {
        return MapDescriptor(descriptor, NamedClass);
    }

    /// <summary>Gets the name of an obfuscated method, or null when the mapping doesn't have it.</summary>
    /// <param name="owner">The obfuscated name of its class.</param>
    /// <param name="name">Its obfuscated name.</param>
    /// <param name="descriptor">Its obfuscated descriptor.</param>
    /// <returns>The name.</returns>
    internal string? NamedMethod(string owner, string name, string descriptor)
    {
        return IsIdentity ? name : _namedMethods.GetValueOrDefault(Key(owner, name, descriptor));
    }

    /// <summary>Gets the obfuscated name of a class, or null when the mapping doesn't have it.</summary>
    /// <param name="named">The name.</param>
    /// <returns>The obfuscated name.</returns>
    internal string? ObfuscatedClass(string named)
    {
        return IsIdentity ? named : _obfuscatedClasses.GetValueOrDefault(named);
    }

    /// <summary>Gets the obfuscated name and descriptor of a method, or null when the mapping doesn't have it.</summary>
    /// <param name="owner">The name of its class.</param>
    /// <param name="method">The method by its name and its descriptor in this mapping's names.</param>
    /// <returns>The obfuscated name and descriptor.</returns>
    internal MethodName? ObfuscatedMethod(string owner, MethodName method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return IsIdentity ? method : _obfuscatedMethods.GetValueOrDefault(Key(owner, method.Name, method.Descriptor));
    }

    // A class in a descriptor, whose name runs from L to the next semicolon; no other part of a descriptor holds an L.
    [GeneratedRegex(pattern: "L(?<name>[^;]+);")]
    private static partial Regex DescriptorClass();

    private static string Group(Match match, string name)
    {
        return match.Groups[name].Value;
    }

    private static string JavaType(string type)
    {
        int dimensions = 0;

        while (type.EndsWith(value: "[]", StringComparison.Ordinal))
        {
            type = type[..^2];
            dimensions++;
        }

        string element = type switch
        {
            "boolean" => "Z",
            "byte" => "B",
            "char" => "C",
            "short" => "S",
            "int" => "I",
            "long" => "J",
            "float" => "F",
            "double" => "D",
            "void" => "V",
            _ => "L" + type.Replace(oldChar: '.', newChar: '/') + ";",
        };

        return new string(c: '[', dimensions) + element;
    }

    private static string Key(string owner, string name, string descriptor)
    {
        return owner + "." + name + descriptor;
    }

    private static IEnumerable<string> Lines(string text)
    {
        return text.Split(separator: '\n').Select(static line => line.TrimEnd(trimChar: '\r')).Where(static line => line.Length > 0);
    }

    private static string Owner(string member)
    {
        return member[..member.LastIndexOf(value: '/')];
    }

    [GeneratedRegex(pattern: @"^(?<named>\S+) -> (?<obfuscated>\S+):$")]
    private static partial Regex ProGuardClass();

    [GeneratedRegex(pattern: @"^\s+(?:\d+:\d+:)?(?<returns>\S+) (?<named>[^\s(]+)\((?<parameters>[^)]*)\)(?::\d+)* -> (?<obfuscated>\S+)$")]
    private static partial Regex ProGuardMethod();

    private static string SimpleName(string member)
    {
        return member[(member.LastIndexOf(value: '/') + 1)..];
    }

    private void AddClass(string obfuscated, string named)
    {
        // Mojang's client and server files name the same classes, which the two jars must obfuscate alike.
        if (_namedClasses.TryGetValue(obfuscated, out string? existing) && existing != named)
            throw new InvalidDataException($"{Source} names {obfuscated} both {existing} and {named}.");

        if (_obfuscatedClasses.TryGetValue(named, out string? other) && other != obfuscated)
            throw new InvalidDataException($"{Source} obfuscates {named} both as {other} and as {obfuscated}.");

        _namedClasses[obfuscated] = named;
        _obfuscatedClasses[named] = obfuscated;
    }

    private void AddMethod(string owner, string name, string descriptor, string named, bool isNamedDescriptor)
    {
        _pending.Add(new PendingMethod(owner, name, descriptor, named, isNamedDescriptor));
    }

    // A descriptor names the classes of the version, so it can only be mapped once every class is known.
    private Mapping Complete()
    {
        foreach (PendingMethod method in _pending)
        {
            string namedOwner = NamedClass(method.Owner) ?? method.Owner;
            string obfuscatedDescriptor = method.IsNamedDescriptor ? MapDescriptor(method.Descriptor, ObfuscatedClass) : method.Descriptor;
            string namedDescriptor = method.IsNamedDescriptor ? method.Descriptor : NamedDescriptor(method.Descriptor);

            _namedMethods[Key(method.Owner, method.Name, obfuscatedDescriptor)] = method.Named;
            _obfuscatedMethods[Key(namedOwner, method.Named, namedDescriptor)] = new MethodName(method.Name, obfuscatedDescriptor);
        }

        _pending.Clear();

        return this;
    }

    private sealed record PendingMethod(string Owner, string Name, string Descriptor, string Named, bool IsNamedDescriptor);
}
