using System.Runtime.CompilerServices;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// Real servers that run as a process of the platform the tests run on, so that the agent of every platform meets a real server and not only reproductions of
/// the bugs: Forge 1.7.2 wherever there is a Java 8, which is everywhere but on Windows for ARM64, and vanilla 1.18.2 on Java 17, which every platform has.
/// </summary>
public sealed class LocalServerTests
{
    // The reload of 1.18.2, SimpleReloadInstance, by the name that release gives it; it waits for its data packs forever when its listener set loses a removal.
    private const string ReloadLocked = "ModLoaderFixes: patched agf (<init>: 1 invocation of com/google/common/collect/Sets.newHashSet redirected)";

    private const string TweakersSorted = "ModLoaderFixes: patched cpw/mods/fml/relauncher/CoreModManager";

    /// <summary>Forge 1.7.2 sorts its tweakers without crashing and becomes ready with the platform's library loaded by <c>-agentpath</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsOnThisPlatformWithAgentPath()
    {
        await AssertStartsAsync(LocalServer.Forge172, PublishedAgent.Find(name: "ModLoaderFixes"), TweakersSorted);
    }

    /// <summary>The same server becomes ready with the jar loaded by <c>-javaagent</c>, which takes the platform's library out of itself.</summary>
    [Fact]
    public async Task Forge172ServerStartsOnThisPlatformWithJavaAgentJar()
    {
        await AssertStartsAsync(LocalServer.Forge172, PublishedAgent.FindJar(name: "ModLoaderFixes"), TweakersSorted);
    }

    /// <summary>Vanilla 1.18.2 becomes ready on Java 17 with its reload's listener set locked, with the platform's library loaded by <c>-agentpath</c>.</summary>
    [Fact]
    public async Task Vanilla1182ServerStartsOnThisPlatformWithAgentPath()
    {
        await AssertStartsAsync(LocalServer.Vanilla1182, PublishedAgent.Find(name: "ModLoaderFixes"), ReloadLocked);
    }

    /// <summary>The same server becomes ready with the jar loaded by <c>-javaagent</c>.</summary>
    [Fact]
    public async Task Vanilla1182ServerStartsOnThisPlatformWithJavaAgentJar()
    {
        await AssertStartsAsync(LocalServer.Vanilla1182, PublishedAgent.FindJar(name: "ModLoaderFixes"), ReloadLocked);
    }

    private static async Task AssertStartsAsync(LocalServer server, PublishedAgent agent, string patched)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Jdk jdk = new(server.Java);

        // A platform without the server's Java, as Windows on ARM64 is without Java 8, skips the test here, before anything is downloaded for the server.
        Assert.True(Directory.Exists(await jdk.GetHomeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));

        DirectoryInfo directory = Directory.CreateTempSubdirectory(prefix: "modloaderfixes-server-");

        try
        {
            string jar = await server.InstallAsync(directory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            JavaRunOptions options = new() { WorkingDirectory = directory.FullName, JvmArguments = ["-Xmx1G"], Arguments = ["nogui"], Timeout = TimeSpan.FromMinutes(minutes: 5) };
            JavaRun run = await RunUntilReadyAsync(jdk, server.MainClass, jar, agent, options, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            ForgeServer.AssertLogged(run.Output, patched);
            ForgeServer.AssertNotLogged(run.Output, unexpected: "ConcurrentModificationException");
            ForgeServer.AssertLogged(run.Output, expected: "Stopping server");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Starts the server, waits until it says it is ready, tells it to stop, as its operator does at its console, and returns what it wrote until it ended.
    private static async Task<JavaRun> RunUntilReadyAsync(Jdk jdk, string mainClass, string jar, PublishedAgent agent, JavaRunOptions options, CancellationToken cancellationToken)
    {
        JavaProcess server = await jdk.StartAsync(mainClass, jar, agent, options, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string started = await server.WaitForOutputAsync(ForgeServer.ReadyLine, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.Contains(ForgeServer.ReadyLine, started, StringComparison.Ordinal);

        await server.WriteLineAsync(line: "stop", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return await server.WaitForExitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }
}
