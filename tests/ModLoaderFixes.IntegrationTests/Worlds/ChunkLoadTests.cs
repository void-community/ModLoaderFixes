using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Worlds;

/// <summary>
/// The published agent in a real JVM, against a fixture of Forge 1.7.2 to 1.9's synchronous chunk load, whose read finds nothing for a chunk that the File
/// IO thread has just taken off its list to write for the first time.
/// </summary>
public sealed class ChunkLoadTests
{
    private const string MainClass = "net.minecraft.world.World";
    private const string Patched = "ModLoaderFixes: patched net/minecraftforge/common/chunkio/ChunkIOExecutor (synchronous chunk loads return the chunk the provider has (1 change))";
    private const string Sources = "Fixtures/ChunkLoad/**/*.java";
    private const string Ticked = "TICKED a chunk that was read and one that was generated, and the provider kept both";

    /// <summary>
    /// Without the agent the load returns null for the chunk that Forge generated in the place of the one it couldn't read, and the world's tick fails on it, as
    /// a server's does with <c>Exception ticking world</c>; with it the load returns that chunk, and a chunk that was read is still the one that was read.
    /// Forge 1.7.2 to 1.9 run on Java 8 and earlier.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentReturnsTheChunkThatForgeGenerated(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "Exception ticking world: java.lang.NullPointerException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarReturnsTheChunkThatForgeGenerated(Jdk jdk)
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
        Assert.Contains(Ticked, patched.Output, StringComparison.Ordinal);
    }

    // The patched load and its moved body go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
