using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ModLoaderFixes.Mappings;

/// <summary>
/// Forge's, Ornithe's and Mojang's mappings, downloaded once into a cache that later builds read them from. A mapping holds every class and method of its
/// release, up to 59 MB of them, and the requests reach 115 mappings, 2 GB together, which is more than a 32-bit process has on Windows, where the x86 .NET SDK
/// runs the generator as one. So the sources keep only the mappings they were last asked for, as many as one release takes, and read another from the cache
/// again when it is asked for later.
/// </summary>
internal sealed class MappingSources : IMappingSources
{
    private const string FirstOfficial = "1.14.4";

    private const string Forge = "https://maven.minecraftforge.net/de/oceanlabs/mcp/";
    // A release is resolved through its own mapping of the requested names and, for each of the two other kinds, through its mapping of that kind and both
    // mappings of the nearest release that has both kinds.
    private const int Kept = 1 + (2 * 3);
    private const string LastTsrg = "1.16.5";
    private const string Manifest = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const string Ornithe = "https://maven.ornithemc.net/releases/";
    private const string OrnitheVersions = "https://meta.ornithemc.net/v3/versions/gen2/intermediary";

    private readonly DirectoryInfo _cache;
    private readonly Downloader _downloader;
    private readonly List<KeptMapping> _kept = [];
    private readonly IReadOnlyDictionary<string, string> _ornithe;
    private readonly Dictionary<string, int> _releaseIndices;
    private readonly IReadOnlySet<string> _srg;
    private readonly IReadOnlySet<string> _tsrg;
    private readonly IReadOnlyDictionary<string, string> _versionFiles;

    private MappingSources(
        Downloader downloader,
        DirectoryInfo cache,
        IReadOnlyList<string> releases,
        IReadOnlyDictionary<string, string> versionFiles,
        IReadOnlySet<string> srg,
        IReadOnlySet<string> tsrg,
        IReadOnlyDictionary<string, string> ornithe
    )
    {
        Dictionary<string, int> indices = new(StringComparer.Ordinal);

        for (int index = 0; index < releases.Count; index++)
            indices[releases[index]] = index;

        _downloader = downloader;
        _cache = cache;
        Releases = releases;
        _releaseIndices = indices;
        _versionFiles = versionFiles;
        _srg = srg;
        _tsrg = tsrg;
        _ornithe = ornithe;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Releases { get; }

    /// <summary>Gets how many mappings the sources hold now.</summary>
    internal int Held => _kept.Count;

    /// <summary>Gets how many times the sources have read a mapping, from the cache or the network.</summary>
    internal int Loads { get; private set; }

    /// <inheritdoc/>
    public async Task<Mapping> GetAsync(Namespace names, string version, CancellationToken cancellationToken)
    {
        string key = Describe(names) + " " + version;

        if (!Has(names, version))
            throw new ArgumentException(version + " has no " + Describe(names) + " mapping.", nameof(version));

        int index = _kept.FindIndex(kept => kept.Key == key);
        KeptMapping used = index < 0 ? new KeptMapping(key, await LoadAsync(names, version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : _kept[index];

        if (index < 0)
            Loads++;
        else
            _kept.RemoveAt(index);

        // The last used mapping goes to the end, and the one that was used longest ago, at the start, goes when there are too many.
        _kept.Add(used);

        if (_kept.Count > Kept)
            _kept.RemoveAt(index: 0);

        return used.Mapping;
    }

    /// <inheritdoc/>
    public bool Has(Namespace names, string version)
    {
        return _releaseIndices.TryGetValue(version, out int index) && names switch
        {
            Namespace.Srg => _srg.Contains(version) || (_tsrg.Contains(version) && index <= _releaseIndices[LastTsrg]),
            Namespace.Intermediary => _ornithe.ContainsKey(version),
            Namespace.Official => index >= _releaseIndices[FirstOfficial],
            _ => false,
        };
    }

    /// <summary>Lists the releases and which mappings each has, from Mojang's version manifest, Forge's Maven metadata and Ornithe's meta server.</summary>
    /// <param name="client">The client that downloads them.</param>
    /// <param name="cache">The directory that keeps the mapping files between runs.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The sources.</returns>
    internal static Task<MappingSources> CreateAsync(HttpClient client, string cache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        return CreateAsync(new Downloader(client, Downloader.Pause), cache, cancellationToken);
    }

    /// <summary>Lists the releases and which mappings each has, as <see cref="CreateAsync(HttpClient, string, CancellationToken)"/> does, through a given downloader.</summary>
    /// <param name="downloader">What downloads the lists and, later, the mappings.</param>
    /// <param name="cache">The directory that keeps the mapping files between runs.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The sources.</returns>
    internal static async Task<MappingSources> CreateAsync(Downloader downloader, string cache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(downloader);

        using JsonDocument manifest = JsonDocument.Parse(await downloader.GetAsync(Manifest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));

        List<JsonElement> releases = [];

        foreach (JsonElement version in manifest.RootElement.GetProperty(propertyName: "versions").EnumerateArray())
        {
            if (Text(version, property: "type") == "release")
                releases.Add(version);
        }

        releases.Sort(static (left, right) => ReleaseTime(left).CompareTo(ReleaseTime(right)));

        Dictionary<string, string> versionFiles = new(StringComparer.Ordinal);

        foreach (JsonElement release in releases)
            versionFiles[Text(release, property: "id")] = Text(release, property: "url");

        HashSet<string> srg = await MavenVersionsAsync(downloader, Forge + "mcp/maven-metadata.xml", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        HashSet<string> tsrg = await MavenVersionsAsync(downloader, Forge + "mcp_config/maven-metadata.xml", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        using JsonDocument ornithe = JsonDocument.Parse(await downloader.GetAsync(OrnitheVersions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));

        Dictionary<string, string> intermediary = new(StringComparer.Ordinal);

        // Before 1.3 the client and the server had mappings of their own; every release since has one for both, which is the one listed for any environment.
        foreach (JsonElement version in ornithe.RootElement.EnumerateArray())
        {
            if (Text(version, property: "environment") == "*")
                intermediary[Text(version, property: "gameVersion")] = Text(version, property: "maven");
        }

        string[] ids = [.. releases.Select(static release => Text(release, property: "id"))];

        return new MappingSources(downloader, new DirectoryInfo(cache), ids, versionFiles, srg, tsrg, intermediary);
    }

    private static string Describe(Namespace names)
    {
        return names switch
        {
            Namespace.Srg => "srg",
            Namespace.Intermediary => "intermediary",
            _ => "official",
        };
    }

    private static async Task<HashSet<string>> MavenVersionsAsync(Downloader downloader, string address, CancellationToken cancellationToken)
    {
        byte[] metadata = await downloader.GetAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        using MemoryStream stream = new(metadata);

        return [.. XDocument.Load(stream).Descendants(name: "version").Select(static version => version.Value)];
    }

    private static DateTimeOffset ReleaseTime(JsonElement version)
    {
        return version.GetProperty(propertyName: "releaseTime").GetDateTimeOffset();
    }

    private static string Text(JsonElement element, string property)
    {
        return element.GetProperty(property).GetString() ?? throw new InvalidDataException(property + " is null.");
    }

    private static string ZipEntry(byte[] zip, string name, string source)
    {
        using ZipArchive archive = new(new MemoryStream(zip), ZipArchiveMode.Read);

        ZipArchiveEntry entry = archive.GetEntry(name) ?? throw new InvalidDataException(source + " has no " + name + ".");

        using StreamReader reader = new(entry.Open(), Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private async Task<byte[]> DownloadAsync(string address, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_cache.FullName, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address))));
        byte[] bytes;

        if (File.Exists(path))
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            bytes = await _downloader.GetAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            _cache.Create();
            await File.WriteAllBytesAsync(path + ".part", bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            File.Move(path + ".part", path, overwrite: true);
        }

        return bytes;
    }

    private async Task<Mapping> LoadAsync(Namespace names, string version, CancellationToken cancellationToken)
    {
        if (names == Namespace.Srg && _srg.Contains(version))
        {
            string address = Forge + "mcp/" + version + "/mcp-" + version + "-srg.zip";
            byte[] zip = await DownloadAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            return Mapping.ReadSrg("Forge's " + version, ZipEntry(zip, name: "joined.srg", address));
        }

        if (names == Namespace.Srg)
        {
            string address = Forge + "mcp_config/" + version + "/mcp_config-" + version + ".zip";
            byte[] zip = await DownloadAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            return Mapping.ReadTsrg("Forge's " + version, ZipEntry(zip, name: "config/joined.tsrg", address));
        }

        return names == Namespace.Intermediary
            ? await LoadOrnitheAsync(version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
            : await LoadMojangAsync(version, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task<Mapping> LoadMojangAsync(string version, CancellationToken cancellationToken)
    {
        byte[] file = await DownloadAsync(_versionFiles[version], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        using JsonDocument document = JsonDocument.Parse(file);

        JsonElement downloads = document.RootElement.GetProperty(propertyName: "downloads");

        // From 26.1 Mojang ships its jars unobfuscated, under the names its mappings used to give them.
        if (!downloads.TryGetProperty(propertyName: "client_mappings", out JsonElement client))
        {
            return version.StartsWith(value: "1.", StringComparison.Ordinal)
                ? throw new InvalidDataException("Mojang has no mappings of " + version + ".")
                : Mapping.Identity("Mojang's " + version);
        }

        List<string> texts = [await MojangTextAsync(client, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)];

        if (downloads.TryGetProperty(propertyName: "server_mappings", out JsonElement server))
            texts.Add(await MojangTextAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));

        return Mapping.ReadProGuard("Mojang's " + version, texts);
    }

    private async Task<Mapping> LoadOrnitheAsync(string version, CancellationToken cancellationToken)
    {
        // Maven coordinates are group:artifact:version, and Calamus publishes the Tiny v2 file under the v2 classifier.
        string[] coordinates = _ornithe[version].Split(separator: ':');
        string artifact = coordinates[1];
        string directory = Ornithe + coordinates[0].Replace(oldChar: '.', newChar: '/') + "/" + artifact + "/" + coordinates[2] + "/";
        string address = directory + artifact + "-" + coordinates[2] + "-v2.jar";
        byte[] jar = await DownloadAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return Mapping.ReadTiny("Ornithe's " + version, ZipEntry(jar, name: "mappings/mappings.tiny", address));
    }

    private async Task<string> MojangTextAsync(JsonElement download, CancellationToken cancellationToken)
    {
        byte[] bytes = await DownloadAsync(Text(download, property: "url"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return Encoding.UTF8.GetString(bytes);
    }

    // A mapping that the sources hold, by the kind of names and the release it was asked for.
    private sealed record KeptMapping(string Key, Mapping Mapping);
}
