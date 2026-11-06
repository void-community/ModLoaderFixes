using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Connections;

/// <summary>
/// The published agent in a real JVM, against fixtures of patchy's read of Mojang's list of blocked servers, whose server sends the first chunk of the list and
/// then resets the connection: patchy 1.3.9's BlockedServers, which Minecraft 1.12 to 1.16.5 check each address with, and patchy 2's supplier, which
/// ServerNameResolver asks from 1.17. Each connects twice from a thread that catches exceptions only, as the connecting screen's does.
/// </summary>
public sealed class BlockedServerListTests
{
    private const string Cause = " from java.io.UncheckedIOException: java.net.SocketException: Connection reset";
    private const string Patchy1 = "net.minecraft.client.multiplayer.GuiConnecting";
    private const string Patchy1Patched =
        "ModLoaderFixes: patched com/mojang/patchy/BlockedServers (<clinit>()V: 1 invocation of java/io/BufferedReader.lines redirected)";
    private const string Patchy1Sources = "Fixtures/BlockList1/**/*.java";
    private const string Patchy2 = "net.minecraft.client.gui.screens.ConnectScreen";
    private const string Patchy2Patched =
        "ModLoaderFixes: patched com/mojang/patchy/MojangBlockListSupplier (createBlockList()Ljava/util/function/Predicate;: 1 invocation of java/io/BufferedReader.lines redirected)";
    private const string Patchy2Sources = "Fixtures/BlockList2/**/*.java";

    /// <summary>
    /// Without the agent patchy 1.3.9's initializer fails with the reset as an ExceptionInInitializerError, which kills the connecting thread, and the next
    /// connection finds the class unusable; with it the list is left empty, as when it can't be fetched, and both connect. Minecraft 1.12 to 1.16.5 run on Java 8.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentConnectsWhenPatchy1CantReadTheList(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        await AssertFixedAsync(
            jdk,
            Patchy1Sources,
            Patchy1,
            "first connection: connector died: java.lang.ExceptionInInitializerError" + Cause
                + "; second connection: connector died: java.lang.NoClassDefFoundError: Could not initialize class com.mojang.patchy.BlockedServers",
            Patchy1Patched,
            patchedResult: "first connection: connected, not blocked; second connection: connected, not blocked"
        );
    }

    /// <summary>
    /// Without the agent patchy 2's supplier lets the reset escape ServerNameResolver's initializer, which kills the connector, and the next connection finds the
    /// resolver unusable; with it the supplier gives no list, as when it can't fetch one, and both connect. Minecraft 1.17 runs on Java 16 and 1.20.5 on Java 21.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17, 21)]
    public async Task AgentConnectsWhenPatchy2CantReadTheList(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        await AssertFixedAsync(
            jdk,
            Patchy2Sources,
            Patchy2,
            "first connection: connector died: java.lang.ExceptionInInitializerError" + Cause
                + "; second connection: connector died: java.lang.NoClassDefFoundError: Could not initialize class net.minecraft.client.multiplayer.resolver.ServerNameResolver",
            Patchy2Patched,
            patchedResult: "first connection: connected, with no list; second connection: connected, with no list"
        );
    }

    /// <summary>The jar for <c>-javaagent</c> fixes patchy 2's read as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task JavaAgentJarConnectsWhenPatchy2CantReadTheList(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Patchy2Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(Patchy2, classPath, jar, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patchy2Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "first connection: connected, with no list; second connection: connected, with no list",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    private static async Task AssertFixedAsync(Jdk jdk, string sources, string mainClass, string unpatchedResult, string patchedLine, string patchedResult)
    {
        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources, jdk.Major, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        JavaRun unpatched = await jdk.RunAsync(mainClass, classPath, options: Verified(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(unpatchedResult, unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(mainClass, classPath, agent, Verified(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(patchedLine, patched.Output, StringComparison.Ordinal);
        Assert.Contains(patchedResult, patched.Output, StringComparison.Ordinal);
    }

    // The patched classes go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
