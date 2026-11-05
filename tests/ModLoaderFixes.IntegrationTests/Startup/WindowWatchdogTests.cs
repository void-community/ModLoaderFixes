using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture that creates the game's window as Forge's early loading window does, with a watchdog that ends the game
/// after 10 seconds while the window takes 12 to come.
/// </summary>
public sealed class WindowWatchdogTests
{
    private const string MainClass = "net.minecraftforge.fml.earlydisplay.DisplayWindow";
    private const string Sources = "Fixtures/WindowWatchdog/**/*.java";

    /// <summary>
    /// Without the agent the watchdog ends the game with <c>Timed out trying to setup the Game Window</c> while it still tries a GL version; with it the watchdog
    /// waits 30 seconds and the window comes. Forge and NeoForge with the early window run on Java 17 and later.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task AgentWaitsForAWindowThatComesAfterTenSeconds(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.Equal(expected: 1, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "Timed out trying to setup the Game Window.", unpatched.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "WINDOW", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task JavaAgentJarWaitsForAWindowThatComesAfterTenSeconds(Jdk jdk)
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
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraftforge/fml/earlydisplay/DisplayWindow (initWindow(Ljava/lang/String;)V: 1 load of constant long 10 replaced)",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(expectedSubstring: "Timed out", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "WINDOW with GL 4.5 after 12 seconds", patched.Output, StringComparison.Ordinal);
    }

    // The patched method goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
