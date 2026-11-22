using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture that loads FML's config twice as Forge and NeoForge do from Minecraft 1.20.1, while an early loading
/// window reads it from its own thread.
/// </summary>
public sealed class ModLoaderConfigurationTests
{
    private const string MainClass = "net.minecraftforge.fml.loading.FMLConfig";
    private const string Sources = "Fixtures/ConfigLoad/**/*.java";

    /// <summary>
    /// Without the agent the window reads the config while the second load has emptied it and crashes; with it the second load is skipped and the window reads
    /// the first one. These clients run on Java 17 and 21, and FML calls loadFrom as classes compiled for them do.
    /// </summary>
    /// <param name="jdk">
    /// The JDK of Java 17 or 21, which compiles the fixture for its own release and runs it without the agent and then with the library.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="jdk"/> is <see langword="null"/>.</exception>
    [Theory]
    [JdkData(17, 21)]
    public async Task AgentLetsTheWindowReadTheConfigurationWhileTheLoaderReadsItAgain(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);

        Assert.Contains(expectedSubstring: "WINDOW CRASHED java.lang.NullPointerException",
            unpatched.Output,
            StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertLoadsTheConfigurationOnce(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK of Java 17, which compiles the fixture for its own release and runs it with the jar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jdk"/> is <see langword="null"/>.</exception>
    [Theory]
    [JdkData(17)]
    public async Task JavaAgentJarLetsTheWindowReadTheConfigurationWhileTheLoaderReadsItAgain(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertLoadsTheConfigurationOnce(patched);
    }

    private static void AssertLoadsTheConfigurationOnce(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);

        // The agent logs the redirect and the members the patch adds on one line.
        Assert.Contains(expectedSubstring: "ModLoaderFixes: patched net/minecraftforge/fml/loading/FMLConfig (load()V: 1 invocation of net/minecraftforge/fml/loading/FMLConfig.loadFrom redirected; modloaderfixes$configData()Ljava/lang/Object;: getter of field configData added; modloaderfixes$loadFrom(Ljava/nio/file/Path;)V: invoker of loadFrom(Ljava/nio/file/Path;)V added)",
            patched.Output,
            StringComparison.Ordinal);

        // The window read the setting the first load stored, and the file was read once.
        Assert.Contains(expectedSubstring: "SQUIR false", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "LOADS 1", patched.Output, StringComparison.Ordinal);
    }

    // The patched class goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
