using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture that reloads as a Minecraft 1.16 to 1.18.2 dedicated server's startup does, with <c>Runnable::run</c> as
/// the game executor, so the workers remove the listeners that reach the reload's barrier from its set at once.
/// </summary>
public sealed class ReloadListenerTests
{
    private const string MainClass = "net.minecraft.server.packs.resources.SimpleReloadInstance";
    private const string Sources = "Fixtures/ReloadListeners/**/*.java";

    /// <summary>
    /// Without the agent a reload's HashSet loses a removal within a few reloads, and that reload never completes; with it the set locks each removal and every
    /// reload completes. Minecraft 1.16 runs on Java 8 and 1.18 on Java 17, so the fixture is compiled for each.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentCompletesEveryReloadWhoseWorkersRemoveListenersAtOnce(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        // On ARM64 every run hung within 61 reloads; on x64 CI one run got through 1000 without a hang, so the fixture reloads 20000 times.
        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "HUNG at reload", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarCompletesEveryReloadWhoseWorkersRemoveListenersAtOnce(Jdk jdk)
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
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/server/packs/resources/SimpleReloadInstance (<init>: 1 invocation of com/google/common/collect/Sets.newHashSet redirected)",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "SET modloaderfixes.ReloadListeners$LockedSet", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "RELOADED 20000 times", patched.Output, StringComparison.Ordinal);
    }

    // The patched constructor goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
