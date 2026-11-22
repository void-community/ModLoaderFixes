using System.Globalization;
using System.IO.Compression;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Worlds;

/// <summary>
/// The published agent in a real JVM, against a fixture of the container of a chunk section's blocks as Minecraft from 1.18 has it, whose read finds the index
/// of a block before the palette has its entry, and against the real containers of pinned servers, which one thread reads while another sets blocks of new
/// kinds in them, as the light engine and generation do.
/// </summary>
public sealed class PaletteReadTests
{
    private const string Blocks = "blocks read again while their palette entry is on its way (1 change))";
    private const string MainClass = "net.minecraft.world.level.chunk.PalettedContainer";
    private const string Patched = "ModLoaderFixes: patched net/minecraft/world/level/chunk/PalettedContainer (" + Blocks;
    private const string Sources = "Fixtures/PaletteRead/**/*.java";

    /// <summary>
    /// Gets the real servers whose containers <see cref="JavaAgentJarReadsEveryBlockOfARealContainerWhileKindsAreAdded"/> reads: 1.18.2, whose names are
    /// obfuscated and whose strategy is a field, and 26.1, which ships Mojang's names and whose strategy a factory makes.
    /// </summary>
    /// <value>
    /// One row for each server: the Java release it runs on, the address Mojang serves its jar at, the jar's SHA-256 checksum, the name the jar gives the
    /// container and the names the harness reaches the container by, each as <c>property=name</c>.
    /// </value>
    public static TheoryData<int, string, string, string, string[]> Servers { get; } = new()
    {
        {
            17,
            "https://piston-data.mojang.com/v1/objects/c8f83c5655308435b3dcf03c06d9fe8740a77469/server.jar",
            "57be9d1e35aa91cfdfa246adb63a0ea11a946081e0464d08bc3d36651718a343",
            "crh",
            ["container=crh", "strategy=crh$e", "strategyField=d", "idMap=gu", "missing=cre", "get=a", "set=b"]
        },
        {
            25,
            "https://piston-data.mojang.com/v1/objects/3872a7f07a1a595e651aef8b058dfc2bb3772f46/server.jar",
            "98ab064389a8b34d48ac3d4c5ed858dd9433d0dfd1a12e1cbbda916448d92994",
            "net/minecraft/world/level/chunk/PalettedContainer",
            [
                "container=net.minecraft.world.level.chunk.PalettedContainer",
                "strategy=net.minecraft.world.level.chunk.Strategy",
                "strategyFactory=createForBlockStates",
                "idMap=net.minecraft.core.IdMap",
                "missing=net.minecraft.world.level.chunk.MissingPaletteEntryException",
                "get=get",
                "set=getAndSetUnchecked",
            ]
        },
    };

    /// <summary>
    /// Without the agent the read of a block whose index is in the storage throws while the palette's new size is on its way and gives <see langword="null"/> while its new
    /// value is; with it the container reads again and gets the block. Minecraft from 1.18 runs on Java 17, 21 and 25.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jdk"/> is <see langword="null"/>.</exception>
    [Theory]
    [JdkData(17, 21, 25)]
    public async Task AgentReadsABlockAgainWhileItsPaletteEntryIsOnItsWay(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);

        Assert.Contains(expectedSubstring: "MISSED the block while the palette's size was on its way",
            unpatched.Output,
            StringComparison.Ordinal);

        Assert.Contains(expectedSubstring: "Missing Palette entry for index 1.", unpatched.Output, StringComparison.Ordinal);

        Assert.Contains(expectedSubstring: "NULL block while the palette's value was on its way",
            unpatched.Output,
            StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertReadsEveryBlock(patched, Patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jdk"/> is <see langword="null"/>.</exception>
    [Theory]
    [JdkData(17, 21, 25)]
    public async Task JavaAgentJarReadsABlockAgainWhileItsPaletteEntryIsOnItsWay(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertReadsEveryBlock(patched, Patched);
    }

    /// <summary>
    /// With the agent's jar, a thread reads every block of the real container of each server through 20000 meetings with a thread that sets blocks of new
    /// kinds in it. Without the agent the read misses a block within some hundred meetings on the ARM64 machine this was written on, with the release's
    /// MissingPaletteEntryException or a <see langword="null"/>; an x86 processor shows a reader its stores in order and can't miss one, so the run without the agent may end
    /// either way and only has to end as one of the two. What the read does with a late entry on every processor is the fixture's to show.
    /// </summary>
    /// <param name="java">The Java release that the server runs on.</param>
    /// <param name="address">Where Mojang serves the server's jar.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="container">The container's name in that jar.</param>
    /// <param name="names">The names the harness reaches the container by, as <c>property=name</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    [Theory]
    [MemberData(nameof(Servers))]
    public async Task JavaAgentJarReadsEveryBlockOfARealContainerWhileKindsAreAdded(int java, string address, string sha256, string container, string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        const string mainClass = "paletterace.PaletteRace";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Jdk jdk = new(java);

        // A platform without the server's Java skips the test here, before the server is downloaded.
        Assert.True(Directory.Exists(await jdk.GetHomeAsync(cancellationToken)));

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        string server = await new PinnedArtifact(new Uri(address), sha256).GetPathAsync(cancellationToken);
        string harness = await jdk.CompileAsync(sources: "Fixtures/PaletteRace/**/*.java", java, cancellationToken);

        DirectoryInfo directory = Directory.CreateTempSubdirectory(prefix: "modloaderfixes-palette-");

        try
        {
            string classPath = string.Join(Path.PathSeparator, value: [harness, .. await UnpackAsync(server, directory, cancellationToken)]);

            JavaRun unpatched = await jdk.RunAsync(mainClass,
                classPath,
                options: Named(names),
                cancellationToken: cancellationToken);

            Assert.True(unpatched.ExitCode is 0 or 1, unpatched.Output);

            Assert.True(unpatched.Output.Contains(value: "READ every block", StringComparison.Ordinal) || unpatched.Output.Contains(value: "MISSED a block", StringComparison.Ordinal),
                unpatched.Output);

            JavaRun patched = await jdk.RunAsync(mainClass, classPath, jar, Named(names), cancellationToken);

            AssertReadsEveryBlock(patched, "ModLoaderFixes: patched " + container + " (" + Blocks);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void AssertReadsEveryBlock(JavaRun patched, string line)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(line, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "READ every block", patched.Output, StringComparison.Ordinal);
    }

    private static JavaRunOptions Named(string[] names)
    {
        JavaRunOptions options = Verified();

        foreach (string name in names)
            options.JvmArguments.Add("-D" + name);

        return options;
    }

    // A server jar from 1.18 holds the game and its libraries as jars of their own, which its launcher unpacks; the container needs them on the class path.
    private static async Task<List<string>> UnpackAsync(string server, DirectoryInfo directory, CancellationToken cancellationToken)
    {
        List<string> jars = [];

        ZipArchive archive = await ZipFile.OpenReadAsync(server, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await using (archive.ConfigureAwait(continueOnCapturedContext: false))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                bool isNested = entry.FullName.StartsWith(value: "META-INF/versions/", StringComparison.Ordinal)
                    || entry.FullName.StartsWith(value: "META-INF/libraries/", StringComparison.Ordinal);

                if (!isNested || !entry.FullName.EndsWith(value: ".jar", StringComparison.Ordinal))
                    continue;

                string jar = Path.Combine(directory.FullName, jars.Count.ToString(CultureInfo.InvariantCulture) + "-" + entry.Name);

                await entry.ExtractToFileAsync(jar, overwrite: true, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                jars.Add(jar);
            }
        }

        return jars;
    }

    // The patched read, with its lambda, goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
