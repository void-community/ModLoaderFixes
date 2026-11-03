using System.Globalization;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Every patch of the agent against the real jars of the Forge, NeoForge, FML, EventBus and ModLauncher versions at the edges of what each fix covers.</summary>
public sealed class RealJarTests
{
    private const string EventListenerHelper = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string FmlHandshakeHandler = "net/minecraftforge/fml/network/FMLHandshakeHandler";
    private const string Forge = "https://maven.minecraftforge.net";
    private const string ForgeClientHandler = "cpw/mods/fml/client/FMLClientHandler";

    private const string ForgeConfiguration = "net/minecraftforge/fml/loading/FMLConfig";
    private const string HandshakeHandler = "net/minecraftforge/network/HandshakeHandler";
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
        // The handshake handler under each of its names, with its list of sent messages and, from 1.14, the order of its reply handlers, which 1.13.2 builds in
        // NetworkInitialization; and the object holder registry, which keeps its HashSet up to 1.21.4.
        {
            Forge, "net.minecraftforge:forge:1.13.2-25.0.223:universal", "2a346876d4ea5dbfec7ae7fb88ac69fded58a2c504baa7a63156fe1fc3253786",
            [FmlHandshakeHandler + " +0", "net/minecraftforge/fml/network/NetworkInitialization +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.16.5-36.2.39:universal", "2aa41930bca8ed63f460d0e05185a64f247342aece121cd5255c8018b76ab33c",
            [FmlHandshakeHandler + " +0", FmlHandshakeHandler + " +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.17.1-37.1.1:universal", "2cc032ae95d60f03e62be4929f3ecd66354e6ed135fa3cf676ced2b2f7910a6d",
            ["net/minecraftforge/fmllegacy/network/FMLHandshakeHandler +0", "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.20.1-47.4.28:universal", "9a6e4341cb5fc92ac6efdea4a9bc7e83cdec75c0f996f9af376c58af7ea7bf97",
            [HandshakeHandler + " +0", HandshakeHandler + " +0", ObjectHolderRegistry + " +0"]
        },
        {
            NeoForged, "net.neoforged:forge:1.20.1-47.1.106:universal", "af67e430f39dc742713c94975166fac29c1a49f5ba7c31095a77289931399dc1",
            [HandshakeHandler + " +0", HandshakeHandler + " +0", ObjectHolderRegistry + " +0"]
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
    /// Vanilla servers, which Mojang serves at the address of its SHA-1, with the obfuscated names of what the patches change, which they take from the mappings of
    /// each release: every 1.7 release's NetworkManager and channel initializer; the NetworkManager of 1.8 to 1.14.3 at the edges of its namings, whose tick
    /// needs no fix; and 1.14.4, whose flush needs none either.
    /// </summary>
    public static TheoryData<string, string, string, string[]> VanillaServers { get; } = new()
    {
        { "1.7.2", "3716cac82982e7c2eb09f83028b555e9ea606002", "b4139899700c1bdbf72880eec4bdb9e46c2cf22d1724a48a018ee0330035462e", ["ef +0", "mt +0"] },
        { "1.7.3", "707857a7bc7bf54fe60d557cca71004c34aa07bb", "027b0bf027910d9ed3c49ff643ee81fa875e95052cd66ab7648a9e602973f266", ["ef +0", "mw +0"] },
        { "1.7.4", "61220311cef80aecc4cd8afecd5f18ca6b9461ff", "796d6ca283861a3185f2e87ec321b1233540cdf2638da6e00f1d96d47791031f", ["ef +0", "mw +0"] },
        { "1.7.5", "e1d557b2e31ea881404e41b05ec15c810415e060", "caa9e13aee32112b3b5305c02ecc01c05502fe244cdb83faa7a01832937542e4", ["eg +0", "nc +0"] },
        { "1.7.6", "41ea7757d4d7f74b95fc1ac20f919a8e521e910c", "5cdc6ab6168ae496ce1d1ae96c0b165360184518d030417429ec7687c0b9d527", ["ej +0", "nc +0"] },
        { "1.7.7", "a6ffc1624da980986c6cc12a1ddc79ab1b025c62", "74646f88ed76d878eaaf2b28c4ebd4043bb11255999e389345dc55d2f11c19a6", ["ej +0", "nc +0"] },
        { "1.7.8", "c69ebfb84c2577661770371c4accdd5f87b8b21d", "5907ef1103acf15952d6d50cde3db01e4fc5a95b9f5fec0be25fa56a5ef0d121", ["ej +0", "nc +0"] },
        { "1.7.9", "4cec86a928ec171fdc0c6b40de2de102f21601b5", "f7b9150d05c2cf8c48541527de310557e6bd9bde73e8ac9479e8ffe722c60a21", ["ej +0", "nc +0"] },
        { "1.7.10", "952438ac4e01b4d115c5fc38f891710c4941df29", "c70870f00c4024d829e154f7e5f4e885b02dd87991726a3308d81f513972f3fc", ["ej +0", "nd +0"] },
        { "1.8", "a028f00e678ee5c6aef0e29656dca091b5df11c7", "40e23f3823d6f0e3cbadc491cedb55b8ba53f8ab516b68182ddd1536babeb291", ["gr +0"] },
        { "1.8.2", "a37bdd5210137354ed1bfe3dac0a5b77fe08fe2e", "d99f3b3478018cb454fbee36fd60e3c4acbe1132f1cb26b3657f9ad291e7035c", ["el +0"] },
        { "1.9.3", "8e897b6b6d784f745332644f4d104f7a6e737ccf", "0eb669eeca23bad6d4ee5237aa24cd51274dfbda15813477f8414f4bfc2f1f27", ["em +0"] },
        { "1.12.2", "886945bfb2b978778c3a0288fd7fab09d315b25f", "fe1f9274e6dad9191bf6e6e8e36ee6ebc737f373603df0946aafcded0d53167e", ["gw +0"] },
        { "1.14.3", "d0d0fe2b1dc6ab4c65554cb734270872b72dadd6", "942256f0bfec40f2331b1b0c55d7a683b86ee40e51fa500a2aa76cf1f1041b38", ["jb +0"] },
        { "1.14.4", "3dc3d84a581f14691199cf6831b71ed1296a9fdf", "5ecdedab3a6e129321a444490d0a467c25ea702a24a99cebe3b6aed41f8f5729", [] },
    };

    /// <summary>
    /// Vanilla clients, which Mojang serves at the address of its SHA-1, with the obfuscated names of what the patches change, which they take from the mappings of
    /// each release: every 1.7 release's PlayerControllerMP, whose tick processes the received packets before it checks the connection, NetworkManager and the two
    /// channel initializers of its integrated server, for the network and in memory; the NetworkManager of 1.8 to 1.14.3 at the edges of its namings, whose tick needs
    /// no fix; and 1.14.4, whose flush needs none either. ChannelAccess, whose clear moves to the sound thread, at the edges of MCP's and Mojang's names from 1.14 to
    /// 1.20.4, and 1.20.5 and the unobfuscated 26.1, which decode with JOrbis. From 1.13 the packet listener whose Disconnect waits for the client thread:
    /// NetHandlerPlayClient up to 1.13.2, whose client is no executor, at each 1.13 release, ClientPlayNetHandler up to 1.16.5, ClientPacketListener up to 1.20.1 and
    /// ClientCommonPacketListenerImpl from 1.20.2, and 26.1's chunk batch answer.
    /// </summary>
    public static TheoryData<string, string, string, string[]> VanillaClients { get; } = new()
    {
        { "1.7.2", "0c8689f904922af71c7144dcfb81bce976cadd49", "507fb3660e77ab1a3000424d9b08c61606ae1e5a9be41caa3728bddd9597365f", ["biy +0", "ef +0", "mt +0", "mu +0"] },
        { "1.7.3", "feaf10fa9da94c388f2c53a2cf31a14406d0c532", "8b1d90ae662b235072d05dbc27d38ae5fef8b322a21d10302703fd798feb05c5", ["bjz +0", "ef +0", "mw +0", "mx +0"] },
        { "1.7.4", "900950d8e3217b3a42405d1ecf767dcc31239d69", "c51494c52a612648cbcb7ebcdaedbcb61f6cd287f6215f90113a43136cb62526", ["bjz +0", "ef +0", "mw +0", "mx +0"] },
        { "1.7.5", "77aa70ac5054cbf1140b6449b579e950d5050c4a", "5f83b944b59c48ea7fa8f92fefd491ecb6d1e8d6c9b412fc849f6457c8cee27b", ["bkf +0", "eg +0", "nc +0", "nd +0"] },
        { "1.7.6", "6b2c5827994e17f904f0852f962dd6fccfbb8bdc", "b1bde364f26f5fbdf657bb7b2b877c34eac201ac346c402192dd98b0ef322281", ["blo +0", "ej +0", "nc +0", "nd +0"] },
        { "1.7.7", "e520f254a2b496d61839b816d712e238b1243f30", "3e41fd81092a1e9bb7d3d268ebd8c8700b5906065974dc6a962ba8417d57ba73", ["blo +0", "ej +0", "nc +0", "nd +0"] },
        { "1.7.8", "83ae44189888a873f46f7509a87ebdb6dc785741", "09d06a078aaedc075682440a5d87d473ee5ebfb35270aa8f085575f133029a31", ["blq +0", "ej +0", "nc +0", "nd +0"] },
        { "1.7.9", "fbbaae784b1de315a8d08a82c6c345a583fb676b", "38f8d799a9b42fb539ca7250e317dd6546910c8ac7718a720c11aad79780e8d8", ["blq +0", "ej +0", "nc +0", "nd +0"] },
        { "1.7.10", "e80d9b3bf5085002218d4be59e668bac718abbc6", "a4fc2284657544e0f4bcc964f927c2fda3e3a205178ed1d5d58883aaf9780cce", ["bje +0", "ej +0", "nd +0", "ne +0"] },
        { "1.8", "d722504db9de2b47f46cc592b8528446272ae648", "ccccd0a92f76fbb24279691daad9b67f85d80471a37e59e0da76d7754982dfe8", ["gr +0"] },
        { "1.8.2", "a8e71f6c81acfa834d249579a242f5b0852075c1", "5cc5b24312f3d182bf06e7f1a4da3a05ce94be6c861d5c9995c86e6d8811fbe9", ["el +0"] },
        { "1.9.3", "b6985b0d3a0520dfb6f17eeb1e8ba58ce9577061", "ab8fdde789c5b4614ffc98249e04fdcce7dfce320b55215bfe5418608af6a63b", ["em +0"] },
        { "1.12.2", "0f275bc1547d01fa5f56ba34bdc87d981ee12daf", "8ada07da5ee77dad3527bd7278fbd05ee1fc8a597813b216a871a2d7d64cc64f", ["gw +0"] },
        { "1.13", "c0b970952cdd279912da384cdbfc0c26e6c6090b", "d807a5ed9428b6c16b64888fd2b7fa76dcf5c55d06f96f7c0c47762c3fc3e26e", ["cqs +0", "hw +0"] },
        { "1.13.1", "8de235e5ec3a7fce168056ea395d21cbdec18d7c", "f0b4cf6d1a87ae150f87a535651138ca7b82bdad8853f76e7ddbff1dd610ac87", ["crc +0", "hw +0"] },
        { "1.13.2", "30bfe37a8db404db11c7edf02cb5165817afb4d9", "3410887ba652f25792c7675bfaf9140e73b60e93cfbf113a803f8a98cb05c0f9", ["crd +0", "hw +0"] },
        { "1.14.3", "af100b34ec7ef2b8b9cf7775b544d21d690dddec", "4063062677433988e012fba53e42e4c21adb6919ac7f061f19f3c6e75b334e07", ["dho +0", "dxv +0", "jb +0"] },
        { "1.14", "7a762a59345c13af7d87111207a93f5a8607f6c0", "93907cbf0655aa5f10d049efa4d745f4250eecc5d0d5d1268046eec3144f323a", ["dhi +0", "dxo +0", "ja +0"] },
        { "1.14.4", "8c325a0c5bd674dd747d6ebaa4c791fd363ad8a9", "b3b2a798e2d67b566008fe4a03767ae2c7ff3f8c7ba6751e7b71fc7299672d0a", ["dkc +0", "eaj +0"] },
        { "1.16.5", "37fd3c903861eeff3bc24b71eed48f828b5269c8", "00b5ebbc33e95ea88c1ab80601599c9827e9aa861d93ccc2cfcbbfd996e86263", ["dwu +0", "enn +0"] },
        { "1.17", "1cf89c77ed5e72401b869f66410934804f3d6f52", "14122287d4e4256c2d65ec8c0ccfa3b56448495fa3ccabc87d0f0eeb4fa1c30f", ["eji +0", "fav +0"] },
        { "1.20.1", "0c3ec587af28e5a785c0b4a7b8a30f9a8f78f838", "56b71336d2b4fdffd197f56595b0da93e32a946f78f382a299b8f4b92758bb0f", ["fex +0", "fyv +0"] },
        { "1.20.2", "82d1974e75fc984c5ed4b038e764e50958ac61a0", "fa1a19be56a506426308abbc1cad85f299a7fc6dae4335559351ba0246713fda", ["fiu +0", "gdj +0"] },
        { "1.20.4", "fd19469fed4a4b4c15b2d5133985f0e3e7816a8a", "9221ab461a491bf9661cd8e773a5e662aaa43d600fa7970b8c12bbfb0431b838", ["fnp +0", "gix +0"] },
        { "1.20.5", "c6b92b2374a629f20802bb284f98a4ee790e950a", "49d5949adbb1021a7eb00c1b949f9e39934b10249cbf86d25004774d1b3ff510", ["fxt +0"] },
        { "1.21.11", "ba2df812c2d12e0219c489c4cd9a5e1f0760f5bd", "1473c9489ac50fda3c435049a76a70d61a10b8610db27f5ba9d8756b686cd3bd", ["hia +0"] },
        { "26.1", "191771837687b766537a8c4607cb6fad79c533a1", "bc6194c61566b587f196090d730698f2191170f7580adb143e4ff839939d3840", ["net/minecraft/client/multiplayer/ClientCommonPacketListenerImpl +0", "net/minecraft/client/multiplayer/ClientPacketListener +0"] },
    };

    /// <summary>
    /// The patches change only the connection and the sound channels of each vanilla client, under each release's names: its NetworkManager up to 1.14.3, in 1.7 its
    /// initializers and its PlayerControllerMP, its ChannelAccess from 1.14 to 1.20.4, its packet listener from 1.14.4, and from 26.1 its ClientPacketListener's chunk
    /// batch answer.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves the jar under.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="expected">What the patches change, as <c>class +added members</c>.</param>
    [Theory]
    [MemberData(nameof(VanillaClients))]
    public async Task PatchesChangeOnlyTheConnectionAndSoundChannelsOfAVanillaClient(string version, string sha1, string sha256, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        string jar = await VanillaClient(version, sha1, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Order(StringComparer.Ordinal), Changes(jar));
    }

    /// <summary>The patches change only the connection of each vanilla server, under each release's names: its NetworkManager up to 1.14.3, and in 1.7 its initializer.</summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves the jar under.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="expected">What the patches change, as <c>class +added members</c>.</param>
    [Theory]
    [MemberData(nameof(VanillaServers))]
    public async Task PatchesChangeOnlyTheConnectionOfAVanillaServer(string version, string sha1, string sha256, string[] expected)
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

    /// <summary>Pins a vanilla client jar, which Mojang serves under its SHA-1 with the same name for every release.</summary>
    /// <param name="version">The Minecraft version, which names the cached file.</param>
    /// <param name="sha1">The SHA-1 in Mojang's address.</param>
    /// <param name="sha256">The pinned checksum.</param>
    /// <returns>The artifact.</returns>
    internal static PinnedArtifact VanillaClient(string version, string sha1, string sha256)
    {
        return new PinnedArtifact(new Uri("https://launcher.mojang.com/v1/objects/" + sha1 + "/client.jar"), sha256, "minecraft_client." + version + ".jar");
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
