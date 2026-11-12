using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>Real Forge and NeoForge servers, as itzg installs them, whose module loader reads every jar through the JDK's zip file system.</summary>
[Collection(ForgeServer.Collection)]
public sealed class ZipChannelServerTests
{
    /// <summary>
    /// Forge 1.20.1's last build on SecureJarHandler 2.1.6, which leaves the zip file system's channel as the JDK opened it, and the first on 2.1.9, which makes it
    /// uninterruptible itself by casting it to the JDK's FileChannelImpl, as NeoForge does through FML's own SecureJarHandler up to 1.21.8. Renovate updates the
    /// images through the custom manager in .github/renovate.json.
    /// </summary>
    public static TheoryData<string, string, string, string> Builds { get; } = new()
    {
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.20.1", "47.0.1" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.20.1", "47.0.2" },
        { "itzg/minecraft-server:2026.10.0-java21", "NEOFORGE", "1.21.8", "21.8.57" },
    };

    /// <summary>
    /// The agent points the zip file system's channel at its helper, and the server opens each of its jars and becomes ready: on Java 9 and later the helper makes the
    /// JDK's own channel uninterruptible, so SecureJarHandler's cast still finds a FileChannelImpl, and the fixture in <see cref="Startup.ZipInterruptTests"/> covers
    /// the interrupt itself.
    /// </summary>
    /// <param name="image">The itzg image.</param>
    /// <param name="type">itzg's TYPE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="loaderVersion">The Forge or NeoForge build.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerOpensItsJarsThroughTheAgentsChannel(string image, string type, string version, string loaderVersion)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        await using IContainer server = ForgeServer.Create(image, type, version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(type == "NEOFORGE" ? "NEOFORGE_VERSION" : "FORGE_VERSION", loaderVersion)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertNotLogged(logs, unexpected: "ClassCastException");
        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
        ForgeServer.AssertLogged(logs, ForgeServer.ZipChannelPatched);
    }
}
