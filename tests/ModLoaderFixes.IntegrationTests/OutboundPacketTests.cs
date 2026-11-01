using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.7's connecting screen, whose client thread flushes the queue of packets while the connector
/// thread sends the login start, as a client now and then does as it connects: with Forge's NetworkManager and with vanilla's.
/// </summary>
public sealed class OutboundPacketTests
{
    private const string MainClass = "connecting.ConnectingScreen";
    private const string Sources = "Fixtures/OutboundPacket/**/*.java";

    // The managers, under the names the fixture gives them, with the line the agent logs when it patches each.
    private static readonly (string Manager, string Patched)[] Managers =
    [
        ("net.minecraft.network.NetworkManager", "ModLoaderFixes: patched net/minecraft/network/NetworkManager (packets sent in order (2 changes))"),
        ("PacketConnection", "ModLoaderFixes: patched PacketConnection (packets sent in order (2 changes))"),
    ];

    /// <summary>
    /// Without the agent the login start goes out first, and the handshake then reaches the encoder in the login state, with no id there; with it the login start
    /// waits for the tick's flush, and both go out in order. Minecraft 1.7 runs on Java 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentSendsTheHandshakeBeforeTheLoginStart(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        foreach ((string manager, string patched) in Managers)
            await AssertFixedAsync(jdk, agent, manager, patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarSendsTheHandshakeBeforeTheLoginStart(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun run = await jdk.RunAsync(MainClass, classPath, jar, Verified(manager: "net.minecraft.network.NetworkManager"), cancellationToken);

        Assert.True(run.ExitCode is 0, run.Output);
        Assert.Contains(expectedSubstring: "sent [handshake, login start]", run.Output, StringComparison.Ordinal);
    }

    private static async Task AssertFixedAsync(Jdk jdk, PublishedAgent agent, string manager, string patched)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(manager), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(manager + " sent [login start], then handshake: Can't serialize unregistered packet", unpatched.Output, StringComparison.Ordinal);

        JavaRun run = await jdk.RunAsync(MainClass, classPath, agent, Verified(manager), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.True(run.ExitCode is 0, run.Output);
        Assert.Contains(patched, run.Output, StringComparison.Ordinal);
        Assert.Contains(manager + " sent [handshake, login start]", run.Output, StringComparison.Ordinal);
    }

    // The fixture's main class takes the manager to connect with, and the patched manager goes through the full verifier.
    private static JavaRunOptions Verified(string manager)
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");
        options.Arguments.Add(manager);

        return options;
    }
}
