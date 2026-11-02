namespace ModLoaderFixes.Mappings;

/// <summary>A method by its name and descriptor, in whichever kind of names its mapping gives.</summary>
/// <param name="Name">The method's name.</param>
/// <param name="Descriptor">The types of its parameters and result, by the names of their classes in the same kind of names.</param>
internal sealed record MethodName(string Name, string Descriptor);
