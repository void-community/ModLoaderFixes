using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Worlds;

/// <summary>
/// The published agent in a real JVM, against fixtures of Paper 1.14's and Paper 1.17's CraftBlockEntityState, which read the state of a spawner that has a
/// chest's block entity as the timings history does.
/// </summary>
public sealed class PaperBlockEntityStateTests
{
    private const string Replaced = "ModLoaderFixes gave the block its own block entity so that its state can be read";

    /// <summary>
    /// Without the agent the constructor's cast fails, which stops a Paper 1.14 server; with it the spawner gets its own block entity on the server thread, and the
    /// server's log says so. A chest's state is read as before, and a read from another thread still fails without changing the world. Paper 1.14 runs on Java 8
    /// and later.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentReadsTheStateOfASpawnerWithAChestsBlockEntityOnPaper114(Jdk jdk)
    {
        JavaRun patched = await AssertRepairedAsync(
            jdk,
            revision: "v1_14_R1",
            sources: "Fixtures/PaperBlockState/**/*.java",
            entity: "net.minecraft.server.v1_14_R1.TileEntityChest"
        );

        // Paper 1.14 lacks CraftBukkit's own repair, so only the helper's line is in the log.
        Assert.DoesNotContain(expectedSubstring: "Bukkit will attempt to fix this", patched.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Paper 1.17's own repair logs that it will attempt a fix and replaces nothing, so the cast fails there too without the agent; with it the spawner gets its
    /// own block entity. Paper 1.17 runs on Java 16, and 17 is the nearest that the tests have.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task AgentReadsTheStateOfASpawnerWithAChestsBlockEntityOnPaper117(Jdk jdk)
    {
        JavaRun patched = await AssertRepairedAsync(
            jdk,
            revision: "v1_17_R1",
            sources: "Fixtures/PaperBlockState17/**/*.java",
            entity: "net.minecraft.world.level.block.entity.TileEntityChest"
        );

        Assert.Contains(expectedSubstring: "Bukkit will attempt to fix this", patched.Output, StringComparison.Ordinal);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture of Paper 1.14 as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarReadsTheStateOfASpawnerWithAChestsBlockEntity(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources: "Fixtures/PaperBlockState/**/*.java", jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass(revision: "v1_14_R1"), classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched, revision: "v1_14_R1");
    }

    private static void AssertPatched(JavaRun patched, string revision)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            "ModLoaderFixes: patched org/bukkit/craftbukkit/" + revision + "/block/CraftBlockEntityState (<init>(Lorg/bukkit/block/Block;Ljava/lang/Class;)V: 1 invocation of java/lang/Class.cast redirected)",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "READ the spawner's state from its own block entity", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "READ the chest's state; REFUSED the read off the server thread", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "Block at 8, 10, 8 is Block{minecraft:spawner} but has ", patched.Output, StringComparison.Ordinal);
        Assert.Contains(Replaced, patched.Output, StringComparison.Ordinal);
    }

    private static async Task<JavaRun> AssertRepairedAsync(Jdk jdk, string revision, string sources, string entity)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources, jdk.Major, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        JavaRun unpatched = await jdk.RunAsync(MainClass(revision), classPath, options: Verified(), cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains("CRASHED on the spawner: Cannot cast " + entity, unpatched.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Replaced, unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass(revision), classPath, agent, Verified(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        AssertPatched(patched, revision);

        return patched;
    }

    private static string MainClass(string revision)
    {
        return "org.bukkit.craftbukkit." + revision + ".block.CraftBlockEntityState";
    }

    // The patched constructor and the helper, which links against the fixture's classes by the server's names, go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
