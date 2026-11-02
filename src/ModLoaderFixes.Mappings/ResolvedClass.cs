namespace ModLoaderFixes.Mappings;

/// <summary>A requested class with its namings in the releases it was requested in.</summary>
/// <param name="Request">The class, its releases and the methods asked of it.</param>
/// <param name="Namings">Each distinct naming with the releases that share it, in the order of the releases.</param>
internal sealed record ResolvedClass(ClassRequest Request, IReadOnlyList<ReleaseNaming> Namings);
