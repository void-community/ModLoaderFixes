using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose server sends a chat, a Disconnect and its close one after the other, as a server that explains a kick
/// does, to a client that handles them as Minecraft 1.14.4 and later do.
/// </summary>
public sealed class FinalPacketTests
{
    private const string MainClass = "net.minecraft.client.multiplayer.ClientPacketListener";
    private const string Sources = "Fixtures/FinalPackets/**/*.java";

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
