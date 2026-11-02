using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose server thread finishes the handshake between the two halves of the handler of the client's last reply,
/// as Forge's handshake handler can from Minecraft 1.13.2 to 1.20.1.
/// </summary>
public sealed class HandshakeReplyTests
{
    private const string MainClass = "net.minecraftforge.network.HandshakeHandler";
    private const string Sources = "Fixtures/HandshakeReplies/**/*.java";

    /// <summary>
    /// Without the agent the reply's own handler finds no handshake handler and throws NullPointerException; with it the reply is handled before its index is removed,
    /// and the handshake completes after both. Forge's handler runs on Java 8 to 17, so the fixture is compiled for each.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentHandlesTheLastReplyBeforeTheHandshakeCompletes(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DROPPED java.lang.NullPointerException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarHandlesTheLastReplyBeforeTheHandshakeCompletes(Jdk jdk)
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
            expectedSubstring: "ModLoaderFixes: patched net/minecraftforge/network/HandshakeHandler (indexed replies handled before they are marked answered (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "EVENTS [acknowledged 18, removed 18, complete]", patched.Output, StringComparison.Ordinal);
    }

    // The patched indexFirst goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
