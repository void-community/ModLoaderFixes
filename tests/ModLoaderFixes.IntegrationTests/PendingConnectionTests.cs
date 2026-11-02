using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against fixtures of Minecraft 1.7's network tick that runs between the channel initializer adding a new connection and
/// channelActive giving it its channel, as a server thread now and then does: Forge's, PaperSpigot's and vanilla's.
/// </summary>
public sealed class PendingConnectionTests
{
    private const string Kept = "joining kept, answered [login start]";
    private const string MainClass = "net.minecraft.network.NetworkSystem";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/network/NetworkSystem (func_151269_c()V: 1 invocation of net/minecraft/network/NetworkManager.func_150724_d redirected; "
        + "func_151269_c()V: 1 invocation of net/minecraft/network/NetworkManager.func_74428_b redirected)";
    private const string Sources = "Fixtures/PendingConnection/**/*.java";

    /// <summary>
    /// Vanilla's initializer, under the name 1.7.10 gives it, adds a new connection that the tick then drops; with the agent the connection
    /// reaches the tick from a later task of the event loop, after channelActive, and its login start is answered.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentAddsAVanillaConnectionOnceItIsActive(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        await AssertFixedAsync(
            jdk,
            sources: "Fixtures/VanillaConnection/**/*.java",
            mainClass: "NetworkTick",
            dropped: "joining dropped, answered [], ticked 0",
            Kept + ", ticked 1",
            patched: "ModLoaderFixes: patched nd (initChannel(Lio/netty/channel/Channel;)V: new connections added to the network tick once their channel is active (2 changes))"
        );
    }

    /// <summary>
    /// Without the agent that tick drops the new connection, whose login start then goes unanswered; with it the connection waits for a later tick, which answers
    /// it, and a connection whose channel closed is still dropped. Forge for Minecraft 1.7 runs on Java 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentKeepsAConnectionTheTickSeesBeforeItHasAChannel(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "joining dropped, answered []", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>
    /// PaperSpigot's tick keeps a connection while Spigot's preparing flag is set, but a channelActive between its check of the channel and its read of the flag
    /// still drops it; with the agent the tick decides on one read of the channel, and answers the login start once the channel is there.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentKeepsAPaperConnectionActivatedDuringTheTick(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        await AssertFixedAsync(
            jdk,
            sources: "Fixtures/PaperConnection/**/*.java",
            mainClass: "net.minecraft.server.v1_7_R4.ServerConnection",
            dropped: "joining dropped, answered []",
            Kept,
            patched: "ModLoaderFixes: patched net/minecraft/server/v1_7_R4/ServerConnection (c()V: 1 invocation of net/minecraft/server/v1_7_R4/NetworkManager.isConnected redirected"
        );
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsAConnectionTheTickSeesBeforeItHasAChannel(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static async Task AssertFixedAsync(Jdk jdk, string sources, string mainClass, string dropped, string kept, string patched)
    {
        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        JavaRun unpatched = await jdk.RunAsync(mainClass, classPath, options: Verified(), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(dropped, unpatched.Output, StringComparison.Ordinal);

        JavaRun run = await jdk.RunAsync(mainClass, classPath, agent, Verified(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.True(run.ExitCode is 0, run.Output);
        Assert.Contains(patched, run.Output, StringComparison.Ordinal);
        Assert.Contains(kept, run.Output, StringComparison.Ordinal);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "joining kept, answered [login start]; leaving dropped", patched.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "FAILED", patched.Output, StringComparison.Ordinal);
    }

    // The patched tick goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
