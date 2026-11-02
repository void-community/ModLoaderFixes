namespace ModLoaderFixes.Mappings;

/// <summary>A class's obfuscated name in one release, with those of the methods requested of it.</summary>
/// <param name="Name">The class's obfuscated name.</param>
/// <param name="Methods">The obfuscated names and descriptors of the requested methods, in the order of the request.</param>
internal sealed record Naming(string Name, IReadOnlyList<MethodName> Methods)
{
    /// <summary>Gets the naming as text, which two namings share when they are the same.</summary>
    internal string Key => Name + " " + string.Join(separator: ' ', Methods.Select(static method => method.Name + method.Descriptor));
}
