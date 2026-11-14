using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Worlds;

/// <summary>
/// The published agent in a real JVM, against a fixture of the region that Minecraft 1.16 generates a chunk through, which sets a dungeon's spawner where an
/// earlier dungeon's chest already has its block entity.
/// </summary>
public sealed class GenerationBlockEntityTests
{
    private const string MainClass = "net.minecraft.world.gen.WorldGenRegion";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/world/gen/WorldGenRegion (block entity of another block removed where generation replaces the block (1 change))";
    private const string Sources = "Fixtures/GenerationRegion/**/*.java";

    /// <summary>
    /// Without the agent the spawner keeps the chest's block entity, as the dungeon's <c>Failed to fetch mob spawner entity</c> says in a server's log; with it the
    /// region removes the chest's block entity as it sets the spawner, and the spawner gets its own. Either way a chest that is set again keeps what it holds, and
    /// one that a block without a block entity replaces loses its block entity. Minecraft 1.16 runs on Java 8 and later.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentGivesTheSpawnerItsOwnBlockEntity(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "Failed to fetch mob spawner entity: the spawner has the block entity of the chest",
            unpatched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "KEPT the loot of the turned chest; REMOVED the block entity of the replaced chest",
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
    public async Task JavaAgentJarGivesTheSpawnerItsOwnBlockEntity(Jdk jdk)
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
        Assert.Contains(expectedSubstring: "FETCHED the spawner's own block entity", patched.Output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "KEPT the loot of the turned chest; REMOVED the block entity of the replaced chest",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    // The patched region, the method the patch adds to it and its two lambdas go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
