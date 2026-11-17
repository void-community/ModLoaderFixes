using System.Runtime.CompilerServices;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers.Local;

/// <summary>
/// Real servers that run as a process of the platform the tests run on, so that the agent of every platform meets a real server and not only reproductions of
/// the bugs: vanilla 1.18.2 and Forge 1.20.2 on Java 17, which every platform has, and Forge 1.7.2 wherever there is a Java 8, which is everywhere but on
/// Windows for ARM64. Each server is installed once and started twice, with the platform's library loaded by <c>-agentpath</c> and with the jar loaded by
/// <c>-javaagent</c>, which takes the platform's library out of itself.
/// </summary>
public sealed class LocalServerTests
{
    // Forge from 1.20.1 loads its config twice when a client's loading window reads it; the agent points the load at its helper, on a server as on a client.
    private const string ConfigurationLoaded = "ModLoaderFixes: patched net/minecraftforge/fml/loading/FMLConfig (load()V: 1 invocation of net/minecraftforge/fml/loading/FMLConfig.loadFrom redirected";

    // The reload of 1.18.2, SimpleReloadInstance, by the name that release gives it; it waits for its data packs forever when its listener set loses a removal.
    private const string ReloadLocked = "ModLoaderFixes: patched agf (<init>: 1 invocation of com/google/common/collect/Sets.newHashSet redirected)";

    private const string TweakersSorted = "ModLoaderFixes: patched cpw/mods/fml/relauncher/CoreModManager";

    /// <summary>Forge 1.20.2, which its own installer installs, becomes ready with FML's config loaded through the agent.</summary>
    [Fact]
    public async Task Forge1202ServerStartsOnThisPlatformWithTheLibraryAndWithTheJar()
    {
        await AssertStartsAsync(LocalServer.Forge1202, ConfigurationLoaded);
    }

    /// <summary>Forge 1.7.2 sorts its tweakers without crashing and becomes ready.</summary>
    [Fact]
    public async Task Forge172ServerStartsOnThisPlatformWithTheLibraryAndWithTheJar()
    {
        await AssertStartsAsync(LocalServer.Forge172, TweakersSorted);
    }

    /// <summary>Vanilla 1.18.2 becomes ready with its reload's listener set locked.</summary>
    [Fact]
    public async Task Vanilla1182ServerStartsOnThisPlatformWithTheLibraryAndWithTheJar()
    {
        await AssertStartsAsync(LocalServer.Vanilla1182, ReloadLocked);
    }

    private static async Task AssertStartsAsync(LocalServer server, string patched)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Jdk jdk = new(server.Java);

        // A platform without the server's Java, as Windows on ARM64 is without Java 8, skips the test here, before anything is downloaded for the server.
        Assert.True(Directory.Exists(await jdk.GetHomeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));

        DirectoryInfo directory = Directory.CreateTempSubdirectory(prefix: "modloaderfixes-server-");

        try
        {
            LocalServerLaunch launch = await server.InstallAsync(directory, jdk, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            await AssertStartsWithAsync(PublishedAgent.Find(name: "ModLoaderFixes"), jdk, launch, patched, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            await AssertStartsWithAsync(PublishedAgent.FindJar(name: "ModLoaderFixes"), jdk, launch, patched, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Starts the server, waits until it says it is ready, tells it to stop, as its operator does at its console, and checks what it wrote until it ended.
    private static async Task AssertStartsWithAsync(PublishedAgent agent, Jdk jdk, LocalServerLaunch launch, string patched, CancellationToken cancellationToken)
    {
        // A gigabyte is what a 32-bit JVM can still be given, and enough for a server without players.
        JavaRunOptions options = new()
        {
            WorkingDirectory = launch.Directory,
            JvmArguments = ["-Xmx1G", .. launch.JvmArguments],
            Arguments = [.. launch.Arguments, "nogui"],
            Timeout = TimeSpan.FromMinutes(minutes: 10),
        };

        JavaProcess server = await jdk.StartAsync(launch.MainClass, launch.ClassPath, agent, options, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string started = await server.WaitForOutputAsync(ForgeServer.ReadyLine, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.Contains(ForgeServer.ReadyLine, started, StringComparison.Ordinal);

        await server.WriteLineAsync(line: "stop", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        JavaRun run = await server.WaitForExitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        string loaded = agent.IsJar ? "the jar" : "the library";

        Assert.True(
            run.Output.Contains(patched, StringComparison.Ordinal),
            $"With {loaded} the server didn't log \"{patched}\". It logged:{Environment.NewLine}{run.Output}"
        );
        ForgeServer.AssertNotLogged(run.Output, unexpected: "ConcurrentModificationException");
        ForgeServer.AssertLogged(run.Output, expected: "Stopping server");
    }
}
