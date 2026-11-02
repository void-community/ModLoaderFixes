namespace ModLoaderFixes.Mappings;

/// <summary>A class's naming in one release, with each route through the mappings that gave it.</summary>
/// <param name="Naming">The obfuscated names that every route agreed on.</param>
/// <param name="Routes">The mappings each route went through, a direct one first when the release has one.</param>
internal sealed record Resolution(Naming Naming, IReadOnlyList<string> Routes);
