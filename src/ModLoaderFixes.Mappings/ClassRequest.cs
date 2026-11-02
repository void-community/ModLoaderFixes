namespace ModLoaderFixes.Mappings;

/// <summary>A class that a patch names, with the methods the patch names.</summary>
/// <param name="Property">The property of <c>MappedClasses</c> that holds the class's names.</param>
/// <param name="Name">The class's name.</param>
/// <param name="Methods">The methods of the class that patches name, whose names each release gets in the same order.</param>
internal sealed record ClassRequest(string Property, string Name, IReadOnlyList<MethodName> Methods);
