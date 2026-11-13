using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Rendering;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.14's entity render dispatcher, whose camera a new level's first frame doesn't have when an
/// item pickup particle draws its item through it.
/// </summary>
public sealed class EntityRenderCameraTests
{
    private const string MainClass = "net.minecraft.client.renderer.entity.EntityRendererManager";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/client/renderer/entity/EntityRendererManager (entities drawn only with a camera (1 change))";
    private const string Sources = "Fixtures/EntityRenderCamera/**/*.java";

    /// <summary>
    /// Without the agent the item's renderer reads the camera that the client cleared when it left the last level, and throws; with it the first frame draws nothing
    /// and the next, whose entity pass prepares the dispatcher, draws the item. Minecraft 1.14 runs on Java 8.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentDrawsThePickedUpItemOnlyOnceTheDispatcherHasACamera(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "first frame: java.lang.NullPointerException; next frame: drawn", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarDrawsThePickedUpItemOnlyOnceTheDispatcherHasACamera(Jdk jdk)
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
        Assert.Contains(expectedSubstring: "first frame: skipped; next frame: drawn", patched.Output, StringComparison.Ordinal);
    }

    // The patched dispatcher, with its lambda and its moved body, goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
