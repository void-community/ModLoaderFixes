using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// Real Forge, NeoForge and Paper servers from 1.18, as itzg installs them, which prepare their spawn area with the light engine reading the blocks of
/// chunks that generation still writes to.
/// </summary>
[Collection(ForgeServer.Collection)]
public sealed class PaletteReadServerTests
{
    private const string MojangsName = "net/minecraft/world/level/chunk/PalettedContainer";
    private const string SpigotsName = "net/minecraft/world/level/chunk/DataPaletteBlock";

    /// <summary>
    /// Gets the servers that <see cref="ServerPreparesItsSpawnAreaThroughThePatchedRead"/> starts, each with the name it runs the container of a chunk
    /// section's blocks under.
    /// </summary>
    /// <value>
    /// One row for each server: the itzg image that runs it, itzg's TYPE, the Minecraft version, the Forge or NeoForge build, which is empty for Paper, and
    /// the container's name in that server.
    /// </value>
    /// <remarks>
    /// Forge 1.19.4, the server whose spawn area stopped at 83% on an ARM64 runner, and NeoForge 1.21.4 run the container under Mojang's name with Mojang's
    /// read; Paper 1.19.4 runs it under Spigot's name; and Paper 1.21.4 under Mojang's name with the read that Paper gives it from 1.21.3. Each runs on
    /// itzg's latest release for its Java, which ForgeServer asks Docker Hub for once in each run.
    /// </remarks>
    public static TheoryData<string, string, string, string, string> Builds { get; } = new()
    {
        { "itzg/minecraft-server:stable-java17", "FORGE", "1.19.4", "45.3.0", MojangsName },
        { "itzg/minecraft-server:stable-java21", "NEOFORGE", "1.21.4", "21.4.161", MojangsName },
        { "itzg/minecraft-server:stable-java17", "PAPER", "1.19.4", string.Empty, SpigotsName },
        { "itzg/minecraft-server:stable-java21", "PAPER", "1.21.4", string.Empty, MojangsName },
    };

    /// <summary>
    /// The agent has the container read again, and the server prepares its spawn area through it and becomes ready. The fixture and the real containers in
    /// <see cref="Worlds.PaletteReadTests"/> cover the late entry itself, which a server meets only now and then.
    /// </summary>
    /// <param name="image">The itzg image.</param>
    /// <param name="type">itzg's TYPE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="loaderVersion">The Forge or NeoForge build, and nothing for Paper, whose latest build for the version itzg takes.</param>
    /// <param name="container">The container's name in that server.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerPreparesItsSpawnAreaThroughThePatchedRead(string image, string type, string version, string loaderVersion, string container)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        await using IContainer server = ForgeServer.Create(image, type, version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(type == "NEOFORGE" ? "NEOFORGE_VERSION" : "FORGE_VERSION", loaderVersion)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
        ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched " + container + ForgeServer.PaletteReadsPatched);
        ForgeServer.AssertNotLogged(logs, unexpected: "MissingPaletteEntryException");
        ForgeServer.AssertNotLogged(logs, unexpected: "Palette index out of bounds");
    }
}
