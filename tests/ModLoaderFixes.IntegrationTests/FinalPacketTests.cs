using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against fixtures whose server sends a chat, a Disconnect and its close one after the other, as a server that explains a kick
/// does, to a client that handles them as Minecraft 1.14.4 and later do, to one that handles them as Minecraft 1.13 does, and to one that handles them as
/// Minecraft 1.7 does.
/// </summary>
public sealed class FinalPacketTests
{
    private const string LegacyMainClass = "net.minecraft.client.multiplayer.PlayerControllerMP";
    private const string LegacySources = "Fixtures/FinalPackets17/**/*.java";
    private const string MainClass = "net.minecraft.client.multiplayer.ClientPacketListener";
    private const string ScheduledMainClass = "net.minecraft.client.network.NetHandlerPlayClient";
    private const string ScheduledSources = "Fixtures/FinalPackets13/**/*.java";
    private const string Sources = "Fixtures/FinalPackets/**/*.java";

    /// <summary>
    /// Without the agent a Minecraft 1.13 client, which is busy in a tick as the chat and the Disconnect arrive, finds the connection closed at the end of the tick and
    /// leaves the world, which drops the chat still queued; with it the Disconnect waits until the client has run the chat, and the client keeps the Disconnect's
    /// reason. Minecraft 1.13 runs on Java 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentShowsTheChatBeforeMinecraft113LeavesTheWorld(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(ScheduledSources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(ScheduledMainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DROPPED [] Proxy is shutting down", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(ScheduledMainClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/client/network/NetHandlerPlayClient (packets before a disconnect handled first (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "SHOWN [[CHAT] Stopping proxy...] Proxy is shutting down", patched.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without the agent Minecraft 1.7's client tick finds the connection closed and reports the Disconnect without processing the chat queued before it; with it the
    /// tick processes the chat first, and still reports the Disconnect's reason. Minecraft 1.7 is compiled for Java 6 and runs on Java 6 to 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentShowsTheChatBeforeMinecraft17ReportsTheDisconnect(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(LegacySources, release: 6, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(LegacyMainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DROPPED [[CHAT] Welcome] Proxy is shutting down", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(LegacyMainClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/client/multiplayer/PlayerControllerMP (packets before a disconnect handled first (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "SHOWN [[CHAT] Welcome, [CHAT] Stopping proxy...] Proxy is shutting down",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Without the agent the Render thread drops the chat, because the Disconnect has closed the connection by the time it gets to it; with it the chat is shown and
    /// the client keeps the Disconnect's reason. Minecraft 1.14.4 to 1.16.5 run on Java 8 and later releases on Java 16 to 21, so the fixture is compiled for 8 and 17.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentShowsTheChatBeforeTheDisconnect(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "DROPPED [Ignoring packet due to disconnection: Stopping proxy...] Proxy is shutting down",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarShowsTheChatBeforeTheDisconnect(Jdk jdk)
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
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/client/multiplayer/ClientPacketListener (packets before a disconnect handled first (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "SHOWN [[CHAT] Stopping proxy...] Proxy is shutting down", patched.Output, StringComparison.Ordinal);
    }

    // The new handler goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
