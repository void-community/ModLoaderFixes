using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Startup;

/// <summary>
/// The published agent in a real JVM, against a fixture that reads a jar through the JDK's zip file system while one of its readers is interrupted, as Forge's
/// module loader reads the jars of its layer.
/// </summary>
public sealed class ZipInterruptTests
{
    private const string MainClass = "zipinterrupt.ZipInterrupt";
    private const string Patched = "ZipFileSystem (<init>: 1 invocation of java/nio/file/Files.newByteChannel redirected)";
    private const string Sources = "Fixtures/ZipInterrupt/**/*.java";

    /// <summary>
    /// Without the agent the interrupt closes the zip file system's channel, and the next entry fails with ClosedChannelException on another thread. With it, Java 8's
    /// channel opens again, and the interrupted reader gets its ClosedByInterruptException as before; later releases' channel is uninterruptible, as SecureJarHandler
    /// makes it, and the interrupted reader reads on. Either way the next entry reads. Java 8 has the zip file system in <c>com.sun.nio.zipfs</c>, which Forge 1.13.2
    /// to 1.16.5 run on, and later releases in <c>jdk.nio.zipfs</c>.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(8, 17, 21)]
    public async Task AgentKeepsTheJarReadableAfterAnInterruptedRead(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(jdk), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "next entry: java.nio.channels.ClosedChannelException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(jdk), cancellationToken);

        AssertPatched(patched, jdk);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(17)]
    public async Task JavaAgentJarKeepsTheJarReadableAfterAnInterruptedRead(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(jdk), cancellationToken);

        AssertPatched(patched, jdk);
    }

    private static void AssertPatched(JavaRun patched, Jdk jdk)
    {
        // SecureJarHandler casts the channel to FileChannelImpl on Java 17 and later, which only Java 8's wrapper isn't.
        string expected = jdk.Major == 8
            ? "channel: modloaderfixes.ZipChannels$Reopening; interrupted reader: java.nio.channels.ClosedByInterruptException; next entry: read"
            : "channel: sun.nio.ch.FileChannelImpl; interrupted reader: read past its interrupt; next entry: read";

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expected, patched.Output, StringComparison.Ordinal);
    }

    // The patched zip file system and the helper go through the full verifier, and the fixture reads the zip file system's channel as SecureJarHandler does.
    private static JavaRunOptions Verified(Jdk jdk)
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        if (jdk.Major > 8)
            options.JvmArguments.Add(item: "--add-opens=jdk.zipfs/jdk.nio.zipfs=ALL-UNNAMED");

        return options;
    }
}
