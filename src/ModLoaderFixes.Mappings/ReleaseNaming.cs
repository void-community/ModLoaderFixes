namespace ModLoaderFixes.Mappings;

/// <summary>A naming with the releases that share it.</summary>
/// <param name="Versions">The releases, as <c>1.7.6 to 1.7.10</c>.</param>
/// <param name="Naming">The class's obfuscated names in them.</param>
internal sealed record ReleaseNaming(string Versions, Naming Naming);
