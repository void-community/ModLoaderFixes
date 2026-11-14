using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>A class's names in Forge or in a set of vanilla releases: its own and those of the methods its <c>MappedClass</c> item requests.</summary>
internal sealed class ClassNames
{
    private readonly IReadOnlyList<(string Name, string Descriptor)> _forgeMethods;
    private readonly IReadOnlyList<(string Name, string Descriptor)> _methods;

    /// <summary>Initializes a naming.</summary>
    /// <param name="name">The class's name in this naming.</param>
    /// <param name="versions">The vanilla releases that name it so, or null for Forge's names.</param>
    /// <param name="forgeMethods">The requested methods under Forge, by which patches ask for them.</param>
    /// <param name="methods">The same methods in this naming, in the same order.</param>
    internal ClassNames(
        string name,
        string? versions,
        IReadOnlyList<(string Name, string Descriptor)> forgeMethods,
        IReadOnlyList<(string Name, string Descriptor)> methods
    )
    {
        Name = name;
        Versions = versions;
        _forgeMethods = forgeMethods;
        _methods = methods;
    }

    /// <summary>Gets the class's name in this naming.</summary>
    internal string Name { get; }

    /// <summary>Gets the vanilla releases that name the class so, for example <c>1.7.8 to 1.7.10</c>, or null for Forge's names.</summary>
    internal string? Versions { get; }

    /// <summary>Gets a method of the class in this naming, which is an interface, by its names under Forge.</summary>
    /// <param name="name">The method's name under Forge.</param>
    /// <param name="descriptor">Its descriptor under Forge.</param>
    /// <returns>The method in this naming.</returns>
    internal JavaMethod Interface(string name, string descriptor)
    {
        JavaMethod method = Virtual(name, descriptor);

        return JavaMethod.Interface(method.Owner, method.Name, method.Descriptor);
    }

    /// <summary>
    /// Starts a patch of the class under this naming. Forge's targets the class by name. A vanilla naming filters by name instead, because another release can give
    /// one of its other classes the same obfuscated name; such a class lacks the naming's methods, and a filtered patch skips it quietly, where a patch by name
    /// would log the skip in every release.
    /// </summary>
    /// <returns>An empty patch.</returns>
    internal ClassPatch Patch()
    {
        string name = Name;

        return Versions is null ? ClassPatch.For(name) : ClassPatch.ForMatching(candidate => candidate == name, name + " of vanilla " + Versions);
    }

    /// <summary>Gets an instance method of the class in this naming, by its names under Forge.</summary>
    /// <param name="name">The method's name under Forge.</param>
    /// <param name="descriptor">Its descriptor under Forge.</param>
    /// <returns>The method in this naming.</returns>
    internal JavaMethod Virtual(string name, string descriptor)
    {
        for (int index = 0; index < _forgeMethods.Count; index++)
        {
            if (_forgeMethods[index] == (name, descriptor))
                return JavaMethod.Virtual(Name, _methods[index].Name, _methods[index].Descriptor);
        }

        throw new ArgumentException(name + descriptor + " isn't among the methods that the MappedClass item of " + Name + " requests.", nameof(name));
    }
}
