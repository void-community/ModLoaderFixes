namespace ModLoaderFixes.Mappings;

/// <summary>
/// Finds the obfuscated names of a class and its methods in a release: through the release's own mapping of the requested kind of names when it has one, and
/// through every other kind it has, which the nearest releases before and after it that have both kinds link to the requested names. Forge's names come first,
/// then Ornithe's and then Mojang's; every route that finds the class and all its methods must agree.
/// </summary>
/// <param name="sources">The mappings of each release.</param>
internal sealed class Resolver(IMappingSources sources)
{
    private static readonly Namespace[] Links = [Namespace.Srg, Namespace.Intermediary, Namespace.Official];

    /// <summary>Gets a release's place among the releases.</summary>
    /// <param name="releases">The releases, oldest first.</param>
    /// <param name="version">The release.</param>
    /// <returns>Its index.</returns>
    internal static int IndexOf(IReadOnlyList<string> releases, string version)
    {
        ArgumentNullException.ThrowIfNull(releases);

        for (int index = 0; index < releases.Count; index++)
        {
            if (releases[index] == version)
                return index;
        }

        throw new ArgumentException(version + " isn't a release of Minecraft.", nameof(version));
    }

    /// <summary>Resolves a class and its methods in a release.</summary>
    /// <param name="names">The kind of names the request uses.</param>
    /// <param name="request">The class and the methods asked of it.</param>
    /// <param name="version">The release.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The naming and the routes that gave it.</returns>
    internal async Task<Resolution> ResolveAsync(Namespace names, ClassRequest request, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> routes = [];
        List<Naming> found = [];

        if (sources.Has(names, version))
        {
            Mapping direct = await sources.GetAsync(names, version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (Direct(direct, request) is { } naming)
            {
                routes.Add(direct.Source);
                found.Add(naming);
            }
        }

        foreach (Namespace link in Links.Where(link => link != names && sources.Has(link, version)))
        {
            Mapping target = await sources.GetAsync(link, version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            foreach (string through in Nearest(names, link, version))
            {
                Mapping named = await sources.GetAsync(names, through, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                Mapping linked = await sources.GetAsync(link, through, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                if (Bridge(named, linked, target, request) is { } naming)
                {
                    routes.Add(named.Source + " to " + linked.Source + " to " + target.Source);
                    found.Add(naming);
                }
            }
        }

        if (found.Count == 0)
            throw new InvalidOperationException("No mapping names " + request.Name + " and all its methods in " + version + ".");

        if (found.Select(static naming => naming.Key).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            IEnumerable<string> answers = routes.Zip(found, static (route, naming) => route + " gives " + naming.Key);

            throw new InvalidOperationException("The mappings disagree on " + request.Name + " in " + version + ": " + string.Join(separator: "; ", answers));
        }

        return new Resolution(found[index: 0], routes);
    }

    // The class and its methods by the requested names in another release, then by the names that both releases have, which link the two.
    private static Naming? Bridge(Mapping named, Mapping linked, Mapping target, ClassRequest request)
    {
        string? through = named.ObfuscatedClass(request.Name);
        string? link = through is null ? null : linked.NamedClass(through);
        string? name = link is null ? null : target.ObfuscatedClass(link);

        if (through is null || link is null || name is null)
            return null;

        List<MethodName> methods = [];

        foreach (MethodName method in request.Methods)
        {
            MethodName? obfuscated = named.ObfuscatedMethod(request.Name, method);
            string? linkedName = obfuscated is null ? null : linked.NamedMethod(through, obfuscated.Name, obfuscated.Descriptor);

            MethodName? resolved = obfuscated is null || linkedName is null
                ? null
                : target.ObfuscatedMethod(link, new MethodName(linkedName, linked.NamedDescriptor(obfuscated.Descriptor)));

            if (resolved is null)
                return null;

            methods.Add(resolved);
        }

        return new Naming(name, methods);
    }

    // The class and its methods by a mapping of the requested names.
    private static Naming? Direct(Mapping mapping, ClassRequest request)
    {
        if (mapping.ObfuscatedClass(request.Name) is not { } name)
            return null;

        List<MethodName> methods = [];

        foreach (MethodName method in request.Methods)
        {
            if (mapping.ObfuscatedMethod(request.Name, method) is not { } obfuscated)
                return null;

            methods.Add(obfuscated);
        }

        return new Naming(name, methods);
    }

    // The nearest release before the version and the nearest after it that have both kinds of names.
    private IEnumerable<string> Nearest(Namespace names, Namespace link, string version)
    {
        IReadOnlyList<string> releases = sources.Releases;
        int index = IndexOf(releases, version);
        string? before = releases.Take(index).LastOrDefault(release => sources.Has(names, release) && sources.Has(link, release));
        string? after = releases.Skip(index + 1).FirstOrDefault(release => sources.Has(names, release) && sources.Has(link, release));

        return new[] { before, after }.OfType<string>();
    }
}
