using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Handshakes;

/// <summary>
/// The published agent in a real JVM, against fixtures of NeoForge 1.20.2's handshake handler: its IntArrayList of the messages that wait for an answer, which the
/// server thread adds to while the network thread removes from, and its indexFirst, whose halves each look the handler up while the server thread can end the
/// handshake between them.
/// </summary>
public sealed class NeoForgeHandshakeTests
{
    private const string MainClass = "net.neoforged.neoforge.network.HandshakeHandler";
    private const string RepliesPatched =
        "ModLoaderFixes: patched net/neoforged/neoforge/network/HandshakeHandler (indexed replies handled before they are marked answered (1 change))";
    private const string RepliesSources = "Fixtures/NeoForgeReplies/**/*.java";
    private const string SentMessagesPatched =
        "ModLoaderFixes: patched net/neoforged/neoforge/network/HandshakeHandler (<init>: 1 new it/unimi/dsi/fastutil/ints/IntArrayList replaced by new modloaderfixes/HandshakeIndexes stored in sentMessages)";
    private const string SentMessagesSources = "Fixtures/NeoForgeSentMessages/**/*.java";

    /// <summary>
    /// Without the agent the reply's own handler finds the handshake ended and throws a NullPointerException; with it the reply is handled before its index is
    /// removed, and the handshake ends after both.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task AgentHandlesTheReplyBeforeTheHandshakeEnds(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(RepliesSources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DROPPED java.lang.NullPointerException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertRepliesPatched(patched);
    }

    /// <summary>
    /// Without the agent the network thread's removals lose some of the server thread's adds, as a lost add ends NeoForge's handshake before the client's last
    /// answer; with it every answer finds its index. NeoForge for Minecraft 1.20.2 runs on Java 17 and later.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17, 21)]
    public async Task AgentKeepsEveryMessageThatWaitsForAnAnswer(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(SentMessagesSources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        // In 11 runs on Java 25, 8 of them limited to the 4 CPUs of a CI runner, every one lost between 6 and 50 of the 400000 messages.
        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "LOST ", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(SentMessagesPatched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "ANSWERED 400000 handshake messages with modloaderfixes.HandshakeIndexes",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the replies as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task JavaAgentJarHandlesTheReplyBeforeTheHandshakeEnds(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(RepliesSources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertRepliesPatched(patched);
    }

    private static void AssertRepliesPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(RepliesPatched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(SentMessagesPatched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "EVENTS [acknowledged 19, removed 19, complete]", patched.Output, StringComparison.Ordinal);
    }

    // The patched handler and the helper go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
