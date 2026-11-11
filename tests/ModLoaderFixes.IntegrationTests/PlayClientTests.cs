using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture of Forge 1.7's wait for the play client, whose network thread waits while the client thread is busy for
/// longer than Forge allows, as a Forge 1.7 client's is on a loaded machine or right after a previous connection ended.
/// </summary>
public sealed class PlayClientTests
{
    private const string MainClass = "cpw.mods.fml.client.FMLClientHandler";
    private const string Patched =
        "ModLoaderFixes: patched cpw/mods/fml/client/FMLClientHandler (waitForPlayClient()V: 1 invocation of java/util/concurrent/CountDownLatch.await redirected; modloaderfixes$playClientBlock()Ljava/util/concurrent/CountDownLatch;: getter of field playClientBlock added)";
    private const string Sources = "Fixtures/PlayClient/**/*.java";

    /// <summary>
    /// Without the agent the network thread gives the join up after a second; with it the network thread waits until the client thread catches up, and stops
    /// waiting at once when the client connects again before it does, which replaces the latch; the dispatcher's own wait then tells which connection it was.
    /// Forge for Minecraft 1.7 runs on Java 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentWaitsForABusyClientThread(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "busy client thread: Timeout waiting for client thread to catch up!",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarWaitsForABusyClientThread(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "busy client thread: joined; abandoned connection: given up", patched.Output, StringComparison.Ordinal);
    }

    // The patched wait goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
