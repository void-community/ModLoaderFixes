using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.7.10's SoundManager, by Forge's names, which reloads its sound system while the first one
/// still loads, over a sound system that lets one of them hold OpenAL's context at a time, as LWJGL 2 does.
/// </summary>
public sealed class SoundLoaderTests
{
    private const string MainClass = "net.minecraft.client.audio.SoundManager";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/client/audio/SoundManager (func_148608_i()V: 1 invocation of java/lang/Thread.start redirected; func_148596_a()V: Before advice added)";
    private const string Sources = "Fixtures/SoundLoader/**/*.java";

    /// <summary>
    /// Without the agent the reload starts a second loader, whose sound system can't get the context and destroys OpenAL, and the first one's next call fails with
    /// UnsatisfiedLinkError; with it the reload waits for the first load and unloads it before it loads again. Forge for Minecraft 1.7 runs on Java 8.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8)]
    public async Task AgentReloadsTheSoundSystemOnceItHasLoaded(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "Only one OpenAL context may be instantiated at any one time.", unpatched.Output, StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: "CRASHED java.lang.UnsatisfiedLinkError: org.lwjgl.openal.AL10.nalGetSourcei(II)I",
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
    public async Task JavaAgentJarReloadsTheSoundSystemOnceItHasLoaded(Jdk jdk)
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
        Assert.DoesNotContain(expectedSubstring: "Only one OpenAL context", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "PLAYING after 2 loads, one at a time", patched.Output, StringComparison.Ordinal);
    }

    // The patched manager and the helper go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
