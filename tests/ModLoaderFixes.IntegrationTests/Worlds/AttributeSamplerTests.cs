using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Worlds;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.21.11's sampler of a dimension's environment attributes, whose server thread clears the tick
/// cache while world generation workers read the moon phase through it.
/// </summary>
public sealed class AttributeSamplerTests
{
    private const string MainClass = "net.minecraft.world.attribute.EnvironmentAttributeSystem";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/world/attribute/EnvironmentAttributeSystem$ValueSampler (dimension values read from the cache once (1 change))";
    private const string Sources = "Fixtures/AttributeSampler/**/*.java";

    /// <summary>
    /// Without the agent the getter's second read of the cache finds it cleared and returns null; with it the getter reads the cache once. Minecraft 1.21.11 runs on
    /// Java 21.
    /// </summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(21)]
    public async Task AgentReadsTheMoonPhaseFromTheCacheOnce(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Interpreted(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "NULL moon phase", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Interpreted(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    /// <param name="jdk">The JDK that compiles and runs the fixture.</param>
    [Theory]
    [JdkData(21)]
    public async Task JavaAgentJarReadsTheMoonPhaseFromTheCacheOnce(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Interpreted(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "READ the moon phase every time", patched.Output, StringComparison.Ordinal);
    }

    // The getter runs interpreted, as its bytecode reads, through the full verifier. Compiled hot in the fixture's loop, the JIT can fold its two reads of the field
    // into one, which the server's colder path through world generation doesn't get, and the race would show only now and then.
    private static JavaRunOptions Interpreted()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");
        options.JvmArguments.Add(item: "-XX:CompileCommand=quiet");
        options.JvmArguments.Add(item: "-XX:CompileCommand=exclude,net/minecraft/world/attribute/EnvironmentAttributeSystem$ValueSampler.getDimensionValue");

        return options;
    }
}
