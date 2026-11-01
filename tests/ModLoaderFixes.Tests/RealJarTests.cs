using System.Globalization;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Every patch of the agent against the real jars of the Forge, NeoForge, FML, EventBus and ModLauncher versions at the edges of what each fix covers.</summary>
public sealed class RealJarTests
{
    private const string EventListenerHelper = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string Forge = "https://maven.minecraftforge.net";
    private const string ForgeClientHandler = "cpw/mods/fml/client/FMLClientHandler";

    private const string ForgeConfiguration = "net/minecraftforge/fml/loading/FMLConfig";
    private const string NeoForgeConfiguration = "net/neoforged/fml/loading/FMLConfig";
    private const string NeoForged = "https://maven.neoforged.net/releases";
    private const string ObjectHolderRegistry = "net/minecraftforge/registries/ObjectHolderRegistry";
    private const string TransformerClassWriter = "cpw/mods/modlauncher/TransformerClassWriter";

    /// <summary>
    /// Each pinned jar with what the patches change in it: one entry per class a patch changes, with the number of members that patch adds, so that a class two patches
    /// change, or one that two versions change with different patches, reads differently.
    /// </summary>
    public static TheoryData<string, string, string, string[]> Jars { get; } = new()
    {
        // FML 1.7's wait for the play client, with the getter of its latch, and the tweak sort that Forge for 1.7.2 runs into on Java 8u20 and later and 1.7.10 no longer has.
        {
            Forge, "net.minecraftforge:forge:1.7.2-10.12.2.1161-mc172:universal", "17628bcf403e312f8de275633528c171795fd0b5469ba9e496baf401a2cd1b7e",
            ["cpw/mods/fml/relauncher/CoreModManager +0", ForgeClientHandler + " +1"]
        },
        {
            Forge, "net.minecraftforge:forge:1.7.10-10.13.4.1614-1.7.10:universal", "00d1ca02192c7efb87da95552ebf247021f1e1d642f86d0a03076314def11529",
            [ForgeClientHandler + " +1"]
        },
        // FML's registry rebuilds on the network thread, which Forge for 1.12.2 moved to the client thread itself.
        {
            Forge, "net.minecraftforge:forge:1.12.1-14.22.1.2478:universal", "80dffcf8b6330d4002589ae92463f51c7823ad24a4a1f3a2c331e9743f97d922",
            ["net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4 +0", "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7 +0"]
        },
        { Forge, "net.minecraftforge:forge:1.12.2-14.23.5.2859:universal", "bd8314729d787e35164476d75afb337afb9783917e4f6fa1f77f049adba22f2d", [] },
        // The handshake handler under each of its names, and the object holder registry, which keeps its HashSet up to 1.21.4.
        {
            Forge, "net.minecraftforge:forge:1.13.2-25.0.223:universal", "2a346876d4ea5dbfec7ae7fb88ac69fded58a2c504baa7a63156fe1fc3253786",
            ["net/minecraftforge/fml/network/FMLHandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.17.1-37.1.1:universal", "2cc032ae95d60f03e62be4929f3ecd66354e6ed135fa3cf676ced2b2f7910a6d",
            ["net/minecraftforge/fmllegacy/network/FMLHandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.20.1-47.4.28:universal", "9a6e4341cb5fc92ac6efdea4a9bc7e83cdec75c0f996f9af376c58af7ea7bf97",
            ["net/minecraftforge/network/HandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            NeoForged, "net.neoforged:forge:1.20.1-47.1.106:universal", "af67e430f39dc742713c94975166fac29c1a49f5ba7c31095a77289931399dc1",
            ["net/minecraftforge/network/HandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        { Forge, "net.minecraftforge:forge:1.21.4-54.1.0:universal", "d5537ad2a67aaee88b1d84b58817f62e49d64f0e0b3ea51b8b03b95e17a7dad0", [ObjectHolderRegistry + " +0"] },
        // FML's config: loaded once, with its getter and invoker, and up to 1.19.4 built without autosave and autoreload; NeoForge's FML 4 changed configData's type.
        { Forge, "net.minecraftforge:fmlloader:1.19.4-45.3.0", "1a5469a8646c0b83561fedd828a7605ede8812d329434042489571883e8f89a5", [ForgeConfiguration + " +0", ForgeConfiguration + " +2"] },
        { Forge, "net.minecraftforge:fmlloader:1.20.1-47.4.28", "9c26db7ee5ae50e0d3d493fe2f07dd4a10d61c1e678203af97986e45c269f414", [ForgeConfiguration + " +2"] },
        { NeoForged, "net.neoforged.fancymodloader:loader:2.0.17", "fdb7c4ffd3b03ce5dba32d453a5335630416b4c820ce54eada5cba96e36be625", [NeoForgeConfiguration + " +2"] },
        { NeoForged, "net.neoforged.fancymodloader:loader:4.0.39", "2c5b1386bba0e77c769e95d53eb2edb00773ef3bded5196a44277ade8f112534", [NeoForgeConfiguration + " +2"] },
        // EventBus's listener lists: built in computeIfAbsent up to 0.10.1, under the write lock with the helper's accessors in 0.10.2 to 0.10.4, and fixed from 0.10.5.
        { Forge, "net.minecraftforge:eventbus:0.10.1", "00315a766018bc6f0c4a2e1eb465c866ac194d7a29d820283ba3db57ba763915", [EventListenerHelper + " +0"] },
        { Forge, "net.minecraftforge:eventbus:0.10.3", "eee16845dafb8d3032b246b82b9a542860dc09c067182c02fcb31038a741d028", [EventListenerHelper + " +3"] },
        { Forge, "net.minecraftforge:eventbus:0.10.5", "061c8c32b827425c4d38d18629c4a45e3d4230b2228fb93d15d9ec28e141a262", [] },
        // ModLauncher's class hierarchy cache: shared plain HashMaps in 2 to 5, which Forge for 1.13.2 to 1.15.2 ships, and ConcurrentHashMaps from 8.
        { Forge, "cpw.mods:modlauncher:2.1.5", "d00219b4f85ee3355e630751fef05df9c1dd64b1f33b07834c22c9d6adbef616", [TransformerClassWriter + " +0"] },
        { Forge, "cpw.mods:modlauncher:4.1.0", "a8f8a7f3f45b2077bf5408ec5a35c7fac6fd51a9f81c15354dfff7a1a732a865", [TransformerClassWriter + " +0"] },
        { Forge, "cpw.mods:modlauncher:5.1.2", "d2099e2187317b85118d898c525f3a494bad4daf6187156b4583042ed0160160", [TransformerClassWriter + " +0"] },
        { Forge, "cpw.mods:modlauncher:8.1.3", "4e0d846f75ffd0dd5042c9b1aa86b8fcc758acd27a004c259d26aebc100ffdf2", [] },
    };

    /// <summary>
    /// Every vanilla server of Minecraft 1.7, which Mojang serves at the address of its SHA-1, with the obfuscated name of its channel initializer, which the vanilla
    /// patch finds by its shape; and 1.8, whose tick needs no fix.
    /// </summary>
    public static TheoryData<string, string, string, string[]> VanillaServers { get; } = new()
    {
        { "1.7.2", "3716cac82982e7c2eb09f83028b555e9ea606002", "b4139899700c1bdbf72880eec4bdb9e46c2cf22d1724a48a018ee0330035462e", ["mt +0"] },
        { "1.7.3", "707857a7bc7bf54fe60d557cca71004c34aa07bb", "027b0bf027910d9ed3c49ff643ee81fa875e95052cd66ab7648a9e602973f266", ["mw +0"] },
        { "1.7.4", "61220311cef80aecc4cd8afecd5f18ca6b9461ff", "796d6ca283861a3185f2e87ec321b1233540cdf2638da6e00f1d96d47791031f", ["mw +0"] },
        { "1.7.5", "e1d557b2e31ea881404e41b05ec15c810415e060", "caa9e13aee32112b3b5305c02ecc01c05502fe244cdb83faa7a01832937542e4", ["nc +0"] },
        { "1.7.6", "41ea7757d4d7f74b95fc1ac20f919a8e521e910c", "5cdc6ab6168ae496ce1d1ae96c0b165360184518d030417429ec7687c0b9d527", ["nc +0"] },
        { "1.7.7", "a6ffc1624da980986c6cc12a1ddc79ab1b025c62", "74646f88ed76d878eaaf2b28c4ebd4043bb11255999e389345dc55d2f11c19a6", ["nc +0"] },
        { "1.7.8", "c69ebfb84c2577661770371c4accdd5f87b8b21d", "5907ef1103acf15952d6d50cde3db01e4fc5a95b9f5fec0be25fa56a5ef0d121", ["nc +0"] },
        { "1.7.9", "4cec86a928ec171fdc0c6b40de2de102f21601b5", "f7b9150d05c2cf8c48541527de310557e6bd9bde73e8ac9479e8ffe722c60a21", ["nc +0"] },
        { "1.7.10", "952438ac4e01b4d115c5fc38f891710c4941df29", "c70870f00c4024d829e154f7e5f4e885b02dd87991726a3308d81f513972f3fc", ["nd +0"] },
        { "1.8", "a028f00e678ee5c6aef0e29656dca091b5df11c7", "40e23f3823d6f0e3cbadc491cedb55b8ba53f8ab516b68182ddd1536babeb291", [] },
    };

    /// <summary>The patches change only the channel initializer of each vanilla 1.7 server, whatever its name, and nothing in vanilla 1.8.</summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves the jar under.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="expected">What the patches change, as <c>class +added members</c>.</param>
    [Theory]
    [MemberData(nameof(VanillaServers))]
    public async Task PatchesChangeOnlyTheInitializerOfAVanillaServer(string version, string sha1, string sha256, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        string jar = await VanillaServer(version, sha1, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Order(StringComparer.Ordinal), Changes(jar));
    }

    /// <summary>The patches change exactly the classes each fix was written for in that version, and leave every other class of the jar as it is.</summary>
    /// <param name="repository">The Maven repository.</param>
    /// <param name="coordinates">The artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="expected">What the patches change, as <c>class +added members</c>.</param>
    [Theory]
    [MemberData(nameof(Jars))]
    public async Task PatchesChangeWhatEachFixCoversInARealJar(string repository, string coordinates, string sha256, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        string jar = await new MavenArtifact(repository, coordinates, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Order(StringComparer.Ordinal), Changes(jar));
    }

    /// <summary>Pins a vanilla server jar, which Mojang serves under its SHA-1 with the same name for every release.</summary>
    /// <param name="version">The Minecraft version, which names the cached file.</param>
    /// <param name="sha1">The SHA-1 in Mojang's address.</param>
    /// <param name="sha256">The pinned checksum.</param>
    /// <returns>The artifact.</returns>
    internal static PinnedArtifact VanillaServer(string version, string sha1, string sha256)
    {
        return new PinnedArtifact(new Uri("https://launcher.mojang.com/v1/objects/" + sha1 + "/server.jar"), sha256, "minecraft_server." + version + ".jar");
    }

    private static List<string> Changes(string jar)
    {
        return
        [
            .. PatcherAgent.Patches.SelectMany(patch => patch.ApplyToJar(jar))
                .Where(static result => result.Changed)
                .Select(static result => result.ClassName + " +" + result.AddedMembers.ToString(CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal),
        ];
    }
}
