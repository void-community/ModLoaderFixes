using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture of securejarhandler's provider of union file systems and against the real provider of pinned releases:
/// one thread looks a registered file system up, as a class load does, while another registers more, as mod discovery does.
/// </summary>
public sealed class UnionFileSystemTests
{
    private const string Found = "FOUND the registered file system in every lookup";
    private const string Missed = "MISSED the registered file system";
    private const string Patched = "ModLoaderFixes: patched cpw/mods/niofs/union/UnionFileSystemProvider (<init>()V: 1 new java/util/HashMap replaced by new java/util/concurrent/ConcurrentHashMap stored in fileSystems)";

    /// <summary>
    /// Gets the real providers that <see cref="JavaAgentJarFindsARegisteredFileSystemOfTheRealProvider"/> runs: securejarhandler 2.1.10, which Forge 1.20.1 and
    /// 1.20.2 and NeoForge 20.2 ship, on Java 17, and securejarhandler 3.0.7 of NeoForge 21.0 to 21.5 and FancyModLoader 9.0.2's of NeoForge 21.6 on Java 21.
    /// </summary>
    /// <value>
    /// One row for each provider: the Java release it runs on, the Maven repository that serves it, the artifact's coordinates and the SHA-256 checksum that
    /// pins its jar.
    /// </value>
    public static TheoryData<int, string, string, string> Providers { get; } = new()
    {
        {
            17,
            "https://maven.minecraftforge.net",
            "cpw.mods:securejarhandler:2.1.10",
            "7ec9207f47bc8847a16566b9a3b4ced8d073c49592897eb30ce2aaf643fdfcab"
        },
        {
            21,
            "https://maven.neoforged.net/releases",
            "cpw.mods:securejarhandler:3.0.7",
            "3822fd6daad43a623152c32fe296889c4bee3957f71dab7038e13d2e5db01395"
        },
        {
            21,
            "https://maven.neoforged.net/releases",
            "net.neoforged.fancymodloader:securejarhandler:9.0.2",
            "ad74df789a53a8110f14070ebab00a562879708520ac764bd65d3b73a856e327"
        },
    };

    /// <summary>
    /// Without the agent a lookup of the registered file system fails while the map grows, with FileSystemNotFoundException or with nothing returned, within a
    /// few rounds; with it the map is a ConcurrentHashMap and every lookup of every round finds the file system. securejarhandler runs on Java 16 and later.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jdk"/> is <see langword="null"/>.</exception>
    [Theory]
    [JdkData(17, 21)]
    public async Task AgentFindsARegisteredFileSystemWhileOthersAreRegistered(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        const string mainClass = "cpw.mods.niofs.union.UnionFileSystemProvider";
        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string classPath = await jdk.CompileAsync(sources: "Fixtures/UnionFileSystem/**/*.java", jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(mainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(Missed, unpatched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "the map is a HashMap", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(mainClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(Found, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "the map is a ConcurrentHashMap", patched.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The real provider of each release loses the registered file system without the agent and finds it in every lookup with it, loaded through the agent's
    /// jar as a game loads it.
    /// </summary>
    /// <param name="java">The Java release that the provider runs on.</param>
    /// <param name="repository">The Maven repository.</param>
    /// <param name="coordinates">The artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task JavaAgentJarFindsARegisteredFileSystemOfTheRealProvider(int java, string repository, string coordinates, string sha256)
    {
        const string mainClass = "unionrace.UnionRace";
        Jdk jdk = new(java);
        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string provider = await new MavenArtifact(repository, coordinates, sha256).GetPathAsync(cancellationToken);
        string harness = await jdk.CompileAsync(sources: "Fixtures/UnionRace/**/*.java", java, cancellationToken);

        string classPath = string.Join(Path.PathSeparator, harness, provider);

        JavaRun unpatched = await jdk.RunAsync(mainClass, classPath, options: Opened(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(Missed, unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(mainClass, classPath, jar, Opened(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(Found, patched.Output, StringComparison.Ordinal);
    }

    // The provider's file systems look into the JDK's method handles, which the games' launchers open for them.
    private static JavaRunOptions Opened()
    {
        JavaRunOptions options = Verified();

        options.JvmArguments.Add(item: "--add-opens");
        options.JvmArguments.Add(item: "java.base/java.lang.invoke=ALL-UNNAMED");

        return options;
    }

    // The patched constructor goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
