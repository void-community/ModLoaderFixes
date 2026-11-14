using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Connections;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.7's connection that kicks players over real connections on the loopback interface: it
/// writes a chat packet and the Disconnect packet and closes at once, with the player's next packet unread.
/// </summary>
public sealed class ChannelCloseTests
{
    private const string MainClass = "net.minecraft.network.NetworkManager";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/network/NetworkManager (func_150718_a(Lnet/minecraft/util/IChatComponent;)V: 1 invocation of io/netty/channel/Channel.close redirected)";
    private const string Sources = "Fixtures/ChannelClose/**/*.java";

    /// <summary>
    /// Java from 11 shuts the output of a registered channel down before it closes it, so every player gets the reason there without the agent, and none sees a
    /// reset; with the agent the fixture ends the same way.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17, 21)]
    public async Task AgentLeavesAConnectionThatJavaClosesGracefullyAsItIs(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        const string graceful = "Nagle on: reason received 20 of 20, reset 0 times; Nagle off: reason received 20 of 20, reset 0 times";

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.True(unpatched.ExitCode is 0, unpatched.Output);
        Assert.Contains(graceful, unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(graceful, patched.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without the agent, Java 8 closes the socket at once: Linux resets the connection for the unread packet and throws away the Disconnect packet that Nagle's
    /// algorithm still holds back, so the players whose connection has the algorithm on, as Minecraft 1.7.2 to 1.8 set it, never get the reason. With the agent those
    /// connections have their output shut down first: each player gets the reason and then the end of the stream. A connection with the algorithm off, as
    /// Minecraft from 1.8.1 sets it, has sent the reason by then and is closed as before, with the reset after the reason.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentSendsTheKicksReasonBeforeItClosesTheConnection(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.DoesNotContain(expectedSubstring: "Nagle on: reason received 20 of 20", unpatched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "Nagle off: reason received 20 of 20, reset 20 times", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarSendsTheKicksReasonBeforeItClosesTheConnection(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "Nagle on: reason received 20 of 20, reset 0 times; Nagle off: reason received 20 of 20, reset 20 times",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    // The patched connection and the helper go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
