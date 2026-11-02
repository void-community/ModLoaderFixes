using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose Render thread releases a sound channel while the sound thread decodes the channel's stream, as
/// Minecraft 1.14 to 1.20.4 clients do when they leave a world.
/// </summary>
public sealed class SoundChannelTests
{
    private const string MainClass = "net.minecraft.client.sounds.ChannelAccess";
    private const string Sources = "Fixtures/SoundChannels/**/*.java";

    /// <summary>
    /// Without the agent the stream is closed while the sound thread decodes it; with it the channels are released on the sound thread after the decode. Minecraft
    /// 1.14 to 1.16.5 run on Java 8 and 1.17 to 1.20.4 on Java 16 and 17, so the fixture is compiled for Java 8 and 17, whose lambdas name private methods differently.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentReleasesTheChannelsOnTheSoundThread(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "CRASHED [decoding, closed, decoded freed memory]", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarReleasesTheChannelsOnTheSoundThread(Jdk jdk)
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
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/client/sounds/ChannelAccess (channels released on the sound thread (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "EVENTS [decoding, decoded, closed]", patched.Output, StringComparison.Ordinal);
    }

    // The new clear and its lambda go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
