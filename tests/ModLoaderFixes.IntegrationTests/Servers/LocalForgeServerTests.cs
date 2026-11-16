using System.Runtime.CompilerServices;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// A real Forge 1.7.2 server that runs as a process of the platform the tests run on, so that the agent of every platform that has Java 8 meets a real server:
/// Windows x64 and x86, macOS, Alpine and 32-bit ARM beside Linux x64 and ARM64. Without the agent this server ends with a
/// <c>ConcurrentModificationException</c> as it sorts its tweakers on Java 8.
/// </summary>
public sealed class LocalForgeServerTests
{
    /// <summary>The server sorts its tweakers without crashing and becomes ready with the platform's library loaded by <c>-agentpath</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsOnThisPlatformWithAgentPath()
    {
        await AssertForge172StartsAsync(PublishedAgent.Find(name: "ModLoaderFixes"));
    }

    /// <summary>The same server becomes ready with the jar loaded by <c>-javaagent</c>, which takes the platform's library out of itself.</summary>
    [Fact]
    public async Task Forge172ServerStartsOnThisPlatformWithJavaAgentJar()
    {
        await AssertForge172StartsAsync(PublishedAgent.FindJar(name: "ModLoaderFixes"));
    }

    private static async Task AssertForge172StartsAsync(PublishedAgent agent)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Jdk jdk = new(major: 8);

        // A platform without a Java 8, as Windows on ARM64, skips the test here, before anything is downloaded for the server.
        Assert.True(Directory.Exists(await jdk.GetHomeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));

        DirectoryInfo directory = Directory.CreateTempSubdirectory(prefix: "modloaderfixes-forge172-");

        try
        {
            string universal = await LocalForgeServer.InstallAsync(directory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            JavaRunOptions options = new() { WorkingDirectory = directory.FullName, JvmArguments = ["-Xmx1G"], Arguments = ["nogui"], Timeout = TimeSpan.FromMinutes(minutes: 5) };
            JavaRun run = await RunUntilReadyAsync(jdk, universal, agent, options, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            ForgeServer.AssertLogged(run.Output, expected: "ModLoaderFixes: patched cpw/mods/fml/relauncher/CoreModManager");
            ForgeServer.AssertNotLogged(run.Output, unexpected: "ConcurrentModificationException");
            ForgeServer.AssertLogged(run.Output, expected: "Stopping server");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Starts the server, waits until it says it is ready, tells it to stop, as its operator does at its console, and returns what it wrote until it ended.
    private static async Task<JavaRun> RunUntilReadyAsync(Jdk jdk, string universal, PublishedAgent agent, JavaRunOptions options, CancellationToken cancellationToken)
    {
        JavaProcess server = await jdk.StartAsync(LocalForgeServer.MainClass, universal, agent, options, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string started = await server.WaitForOutputAsync(ForgeServer.ReadyLine, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.Contains(ForgeServer.ReadyLine, started, StringComparison.Ordinal);

        await server.WriteLineAsync(line: "stop", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return await server.WaitForExitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }
}
