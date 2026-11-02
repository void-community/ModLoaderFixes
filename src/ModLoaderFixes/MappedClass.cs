using JvmBridge.Agents;

namespace ModLoaderFixes;

/// <summary>
/// A class of Minecraft that patches name by the names Forge runs it under, MCP's up to 1.16.5 and Mojang's from 1.17, with the names that each vanilla release
/// obfuscated it and the requested methods to. <c>src/ModLoaderFixes.Mappings</c> resolves them from Forge's, Ornithe's and Mojang's mappings into
/// <c>Mappings.g.cs</c>, for the classes and methods that <c>Mappings.txt</c> lists.
/// </summary>
internal sealed class MappedClass
{
    /// <summary>Initializes a class with its names.</summary>
    /// <param name="name">The class's name under Forge.</param>
    /// <param name="methods">The requested methods' names and descriptors under Forge.</param>
    /// <param name="vanilla">For each set of vanilla releases that name them alike, the releases, the class's obfuscated name and its methods', in the same order.</param>
    internal MappedClass(
        string name,
        IReadOnlyList<(string Name, string Descriptor)> methods,
        IReadOnlyList<(string Versions, string Name, IReadOnlyList<(string Name, string Descriptor)> Methods)> vanilla
    )
    {
        Forge = new ClassNames(name, versions: null, methods, methods);
        Vanilla = [.. vanilla.Select(naming => new ClassNames(naming.Name, naming.Versions, methods, naming.Methods))];
    }

    /// <summary>Gets the names Forge runs the class under, which the vanilla releases that ship it unobfuscated share.</summary>
    internal ClassNames Forge { get; }

    /// <summary>Gets the names of the vanilla releases, one for each set of releases that name the class and its methods alike.</summary>
    internal IReadOnlyList<ClassNames> Vanilla { get; }

    /// <summary>Builds a patch for the class under Forge's names and one under each vanilla naming, except those that Forge's patch covers already.</summary>
    /// <param name="patch">Builds the patch for a naming, starting from <see cref="ClassNames.Patch"/>.</param>
    /// <returns>The patches, Forge's first.</returns>
    internal IReadOnlyList<ClassPatch> Patches(Func<ClassNames, ClassPatch> patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return [patch(Forge), .. Vanilla.Where(naming => naming.Name != Forge.Name).Select(patch)];
    }
}
