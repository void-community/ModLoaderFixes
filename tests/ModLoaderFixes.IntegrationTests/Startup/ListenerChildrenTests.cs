using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture of EventBus 3.0.3's listener list, whose workers add the lists of new subclasses to an event's children
/// while others register listeners for the event and rebuild over the children, as mods that construct at once on Forge 1.13.2 to 1.16.4 do.
/// </summary>
public sealed class ListenerChildrenTests
{
    private const string MainClass = "net.minecraftforge.eventbus.ListenerList";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraftforge/eventbus/ListenerList$ListenerListInst (addChild(Lnet/minecraftforge/eventbus/ListenerList$ListenerListInst;)V: 1 new java/util/ArrayList() replaced by modloaderfixes/ListenerChildren.newChildren() stored in children; addChild(Lnet/minecraftforge/eventbus/ListenerList$ListenerListInst;)V: access set to 0x22; forceRebuild()V: 1 invocation of java/util/List.iterator redirected)";
    private const string Sources = "Fixtures/ListenerChildren/**/*.java";

    /// <summary>
    /// Without the agent the rebuilds throw ConcurrentModificationException and children are lost; with it every rebuild iterates a copy and every child is kept.
    /// Forge for Minecraft 1.13.2 to 1.16.4 runs on Java 8.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentKeepsEveryChildWhileTheListRebuilds(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "rebuilds with ConcurrentModificationException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsEveryChildWhileTheListRebuilds(Jdk jdk)
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
        Assert.Contains(
            expectedSubstring: "REBUILT every list while its children grew, kept in java.util.Collections$SynchronizedRandomAccessList",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    // The patched list and the helper go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
