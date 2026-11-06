using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>
/// The typed helpers against the real classes they call, as the agent's patches leave them: every class, field and method they name exists with the descriptor they
/// were compiled against, which their stubs only claim. The other helpers for Forge 1.7 and 1.12, for PaperSpigot and for Mojang's names call classes that no
/// pinned jar holds by those names, so their fixtures and servers cover them instead.
/// </summary>
public sealed class HelperLinkTests
{
    private const string Forge = "https://maven.minecraftforge.net";
    private const string NeoForged = "https://maven.neoforged.net/releases";

    /// <summary>EventBus 0.10.2 to 0.10.4, whose EventListenerHelper the helper reaches through the methods the patch adds.</summary>
    public static TheoryData<string, string> EventBuses { get; } = new()
    {
        { "net.minecraftforge:eventbus:0.10.2", "e313a118c8b1042a2d8e931f5e627696f22b842f174e8fe4422f1bec5aa8f9b2" },
        { "net.minecraftforge:eventbus:0.10.3", "eee16845dafb8d3032b246b82b9a542860dc09c067182c02fcb31038a741d028" },
        { "net.minecraftforge:eventbus:0.10.4", "59f8a341f2016a0c607406028a306a25358dcd0928f5141bfe351b6dcd2e8391" },
    };

    /// <summary>Forge for Minecraft 1.7.2 and 1.7.10, whose FMLClientHandler the play client helper reaches through the getter the patch adds.</summary>
    public static TheoryData<string, string> Forges { get; } = new()
    {
        { "net.minecraftforge:forge:1.7.2-10.12.2.1161-mc172:universal", "17628bcf403e312f8de275633528c171795fd0b5469ba9e496baf401a2cd1b7e" },
        { "net.minecraftforge:forge:1.7.10-10.13.4.1614-1.7.10:universal", "00d1ca02192c7efb87da95552ebf247021f1e1d642f86d0a03076314def11529" },
    };

    /// <summary>Forge's FML and NeoForge's, with configData of each type NeoForge's FML has given it.</summary>
    public static TheoryData<string, string, string, string> Loaders { get; } = new()
    {
        {
            "net.minecraftforge:fmlloader:1.20.1-47.4.28", "9c26db7ee5ae50e0d3d493fe2f07dd4a10d61c1e678203af97986e45c269f414",
            "net.neoforged.fancymodloader:loader:2.0.17", "fdb7c4ffd3b03ce5dba32d453a5335630416b4c820ce54eada5cba96e36be625"
        },
        {
            "net.minecraftforge:fmlloader:1.19.4-45.3.0", "1a5469a8646c0b83561fedd828a7605ede8812d329434042489571883e8f89a5",
            "net.neoforged.fancymodloader:loader:4.0.39", "2c5b1386bba0e77c769e95d53eb2edb00773ef3bded5196a44277ade8f112534"
        },
    };

    /// <summary>The FML config helper, which serves Forge and NeoForge, links against both loaders that the patches changed.</summary>
    /// <param name="forgeCoordinates">Forge's FML loader.</param>
    /// <param name="forgeSha256">Its pinned checksum.</param>
    /// <param name="neoForgeCoordinates">NeoForge's FML loader.</param>
    /// <param name="neoForgeSha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(Loaders))]
    public async Task ConfigurationHelperLinks(string forgeCoordinates, string forgeSha256, string neoForgeCoordinates, string neoForgeSha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string forge = await new MavenArtifact(Forge, forgeCoordinates, forgeSha256).GetPathAsync(cancellationToken);
        string neoForge = await new MavenArtifact(NeoForged, neoForgeCoordinates, neoForgeSha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Modloaderfixes.ModLoaderConfigs, [forge, neoForge], PatcherAgent.Patches, cancellationToken);
    }

    /// <summary>The EventBus helper links against a real EventBus that the patches changed.</summary>
    /// <param name="coordinates">The EventBus artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(EventBuses))]
    public async Task EventBusHelperLinks(string coordinates, string sha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string eventBus = await new MavenArtifact(Forge, coordinates, sha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Modloaderfixes.EventListeners, [eventBus], PatcherAgent.Patches, cancellationToken);
    }

    /// <summary>NeoForge 1.20.2's list of handshake indexes links against the fastutil that Minecraft 1.20.2 to 1.20.4 ship, whose IntArrayList it extends.</summary>
    [Fact]
    public async Task HandshakeIndexesHelperLinks()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string fastutil = await new MavenArtifact(
            repository: "https://libraries.minecraft.net",
            coordinates: "it.unimi.dsi:fastutil:8.5.12",
            sha256: "b5543ee08d062d551cf0a5c9bc0fb70588b0382079029ba48941fa9c9be8a5d4"
        ).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 17), JavaClasses.Modloaderfixes.HandshakeIndexes, [fastutil], PatcherAgent.Patches, cancellationToken);
    }

    /// <summary>The helper that adds vanilla's new connections links against the Netty that vanilla 1.7.2 and 1.7.10 servers bundle, the oldest and newest.</summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves the jar under.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    [Theory]
    [InlineData("1.7.2", "3716cac82982e7c2eb09f83028b555e9ea606002", "b4139899700c1bdbf72880eec4bdb9e46c2cf22d1724a48a018ee0330035462e")]
    [InlineData("1.7.10", "952438ac4e01b4d115c5fc38f891710c4941df29", "c70870f00c4024d829e154f7e5f4e885b02dd87991726a3308d81f513972f3fc")]
    public async Task NewConnectionHelperLinks(string version, string sha1, string sha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string server = await RealJarTests.VanillaServer(version, sha1, sha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Modloaderfixes.NewConnections, [server], PatcherAgent.Patches, cancellationToken);
    }

    /// <summary>The play client helper links against a real Forge 1.7 that the patches changed.</summary>
    /// <param name="coordinates">The Forge artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(Forges))]
    public async Task PlayClientHelperLinks(string coordinates, string sha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string forge = await new MavenArtifact(Forge, coordinates, sha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Modloaderfixes.PlayClients, [forge], PatcherAgent.Patches, cancellationToken);
    }
}
