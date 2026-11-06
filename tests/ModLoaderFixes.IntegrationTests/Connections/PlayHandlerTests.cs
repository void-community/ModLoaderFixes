using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Connections;

/// <summary>
/// The published agent in a real JVM, against a fixture of Forge 1.7's handshake that joins a server twice, whose server answers each time before the client
/// thread has given playClientBlock the new connection's latch.
/// </summary>
public sealed class PlayHandlerTests
{
    private const string MainClass = "cpw.mods.fml.client.FMLClientHandler";
    private const string Patched =
        "ModLoaderFixes: patched cpw/mods/fml/common/network/handshake/NetworkDispatcher (modloaderfixes$netHandler(Lnet/minecraft/network/INetHandler;)V: setter of field netHandler added; clientListenForServerHandshake()V: After advice added)";
    private const string Sources = "Fixtures/PlayHandler/**/*.java";

    /// <summary>
    /// Without the agent the first connection's wait finds no latch and throws a NullPointerException, and the second's returns at once on the first connection's,
    /// so its REGISTER casts the login handler to NetHandlerPlayClient; with it each handshake waits for its own connection's play handler. Forge for Minecraft
    /// 1.7 runs on Java 8.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentWaitsForTheConnectionsOwnPlayHandler(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "first connection: java.lang.NullPointerException; second connection: java.lang.ClassCastException: Cannot cast net.minecraft.client.network.NetHandlerLoginClient to net.minecraft.client.network.NetHandlerPlayClient",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarWaitsForTheConnectionsOwnPlayHandler(Jdk jdk)
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
        Assert.Contains(expectedSubstring: "first connection: joined; second connection: joined", patched.Output, StringComparison.Ordinal);
    }

    // The advised dispatcher and the helper go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
