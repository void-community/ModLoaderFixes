namespace ModLoaderFixes.Mappings;

/// <summary>The mappings of each Minecraft release, by the kind of names they give.</summary>
internal interface IMappingSources
{
    /// <summary>Gets Minecraft's releases, oldest first.</summary>
    IReadOnlyList<string> Releases { get; }

    /// <summary>Gets a release's mapping to a kind of names, which <see cref="Has"/> says it has.</summary>
    /// <param name="names">The kind of names.</param>
    /// <param name="version">The release.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The mapping.</returns>
    Task<Mapping> GetAsync(Namespace names, string version, CancellationToken cancellationToken);

    /// <summary>Gets whether a release has a mapping to a kind of names.</summary>
    /// <param name="names">The kind of names.</param>
    /// <param name="version">The release.</param>
    /// <returns>Whether it has one.</returns>
    bool Has(Namespace names, string version);
}
