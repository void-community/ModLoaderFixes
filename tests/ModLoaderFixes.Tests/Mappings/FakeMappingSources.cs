using ModLoaderFixes.Mappings;

namespace ModLoaderFixes.Tests.Mappings;

/// <summary>Mappings written out in a test, for releases that it names in order.</summary>
/// <param name="releases">The releases, oldest first.</param>
internal sealed class FakeMappingSources(params IReadOnlyList<string> releases) : IMappingSources
{
    private readonly Dictionary<string, Mapping> _mappings = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IReadOnlyList<string> Releases => releases;

    /// <inheritdoc/>
    public Task<Mapping> GetAsync(Namespace names, string version, CancellationToken cancellationToken)
    {
        return Task.FromResult(_mappings[Key(names, version)]);
    }

    /// <inheritdoc/>
    public bool Has(Namespace names, string version)
    {
        return _mappings.ContainsKey(Key(names, version));
    }

    /// <summary>Adds a release's mapping to a kind of names.</summary>
    /// <param name="names">The kind of names the mapping gives.</param>
    /// <param name="version">The release it maps.</param>
    /// <param name="mapping">The release's classes and methods under those names.</param>
    /// <returns>The sources, to add more.</returns>
    internal FakeMappingSources With(Namespace names, string version, Mapping mapping)
    {
        _mappings[Key(names, version)] = mapping;

        return this;
    }

    private static string Key(Namespace names, string version)
    {
        string kind = names switch
        {
            Namespace.Srg => "srg",
            Namespace.Intermediary => "intermediary",
            _ => "official",
        };

        return kind + " " + version;
    }
}
