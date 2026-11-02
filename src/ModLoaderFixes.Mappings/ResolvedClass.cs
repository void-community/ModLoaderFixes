namespace ModLoaderFixes.Mappings;

/// <summary>A requested class with its namings in the releases of its block.</summary>
/// <param name="Request">The class and the methods asked of it.</param>
/// <param name="Block">The block that asked for it, whose releases the namings cover.</param>
/// <param name="Namings">Each distinct naming with the releases that share it, in the order of the releases.</param>
internal sealed record ResolvedClass(ClassRequest Request, RequestBlock Block, IReadOnlyList<ReleaseNaming> Namings);
