using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Making the map of union file systems that securejarhandler's UnionFileSystemProvider keeps a concurrent one, in its constructor.</summary>
public sealed class UnionFileSystemsPatchTests
{
    // The provider's class, the map that the patch has its constructor create for the file systems, and the constructor's name.
    private const string ClassName = "cpw/mods/niofs/union/UnionFileSystemProvider";
    private const string ConcurrentHashMap = "java/util/concurrent/ConcurrentHashMap";
    private const string Constructor = "<init>";

    // The Maven repositories of Forge and of NeoForge, which serve the real providers, and the map that an unpatched provider creates.
    private const string Forge = "https://maven.minecraftforge.net";
    private const string HashMap = "java/util/HashMap";
    private const string NeoForged = "https://maven.neoforged.net/releases";

    /// <summary>
    /// Gets the releases at the ends of each line that has the provider: <c>cpw.mods:securejarhandler</c> from 0.9.0 to 2.1.10 on Forge's Maven and on to 3.0.9
    /// on NeoForge's, FancyModLoader's own from 9.0.0 to 10.0.2, and Forge's securemodules from 2.2.2 to 2.2.24, with those that Forge 1.17.1 and 1.18,
    /// NeoForge 20.2 to 20.4, NeoForge 21.0 to 21.5 and NeoForge 21.6 ship in between.
    /// </summary>
    /// <value>
    /// One row for each release: the address of the Maven repository that serves it, its Maven coordinates, and the SHA-256 checksum that its jar is pinned to.
    /// </value>
    public static TheoryData<string, string, string> Providers { get; } = new()
    {
        { Forge, "cpw.mods:securejarhandler:0.9.0", "a64bc69b323ef16d7e69e15ecc1f4592d9c988397335c3bfd39bdaa33451323e" },
        { Forge, "cpw.mods:securejarhandler:0.9.54", "823c9ff565c3f29013ab17d20a03e5ba178675f1f0d0a0e2b7b8355bbadb07db" },
        { Forge, "cpw.mods:securejarhandler:1.0.8", "69249634131934d346ba788fd099649b2b9277b002fcb612e86e43e18091f075" },
        { Forge, "cpw.mods:securejarhandler:2.1.10", "7ec9207f47bc8847a16566b9a3b4ced8d073c49592897eb30ce2aaf643fdfcab" },
        { NeoForged, "cpw.mods:securejarhandler:2.1.11", "44d287cb7baf723cc7000b9121833981db7f418451e648acb7582936427d02cc" },
        { NeoForged, "cpw.mods:securejarhandler:2.1.23", "c9574bcb5ed44875102500cef00d47c6562221bef8bd93cade53256fcbb2db43" },
        { NeoForged, "cpw.mods:securejarhandler:3.0.7", "3822fd6daad43a623152c32fe296889c4bee3957f71dab7038e13d2e5db01395" },
        { NeoForged, "cpw.mods:securejarhandler:3.0.9", "80efbd397f06cd16de23e390607a4ce2180787f635440d71918fa78506a084df" },
        {
            NeoForged,
            "net.neoforged.fancymodloader:securejarhandler:9.0.0",
            "81be74922791aa8e95a5a88d59942aedaacc9abb3841461bb0c92baf68d92d9c"
        },
        {
            NeoForged,
            "net.neoforged.fancymodloader:securejarhandler:9.0.2",
            "ad74df789a53a8110f14070ebab00a562879708520ac764bd65d3b73a856e327"
        },
        {
            NeoForged,
            "net.neoforged.fancymodloader:securejarhandler:9.0.18",
            "6785ba714b71b1c845482e58272a7d59ace0857b38ebe01849dc07a6cc3e1ea9"
        },
        {
            NeoForged,
            "net.neoforged.fancymodloader:securejarhandler:10.0.2",
            "bc0b41cde2c61696a365362b0549dcf44b3bf78515cd683c2a9a2edd137eeb97"
        },
        { Forge, "net.minecraftforge:securemodules:2.2.2", "46dc90dae6d626d8dc2bb1cff227b7a64e40d4cfa7097dfaa0ef287559586eb4" },
        { Forge, "net.minecraftforge:securemodules:2.2.3", "2c94fd5e3d5f3c19957aac12cdddd4b5d2b9487750e7e671ad10e602765aa280" },
        { Forge, "net.minecraftforge:securemodules:2.2.24", "40cb1fa55f48e3812eb3f823019cf906d9995bc4c9015f3f8f28471315c2a656" },
    };

    /// <summary>The map stored in <c>fileSystems</c> is a ConcurrentHashMap, and a map the constructor stores elsewhere stays a HashMap.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left the provider whose constructor stores a HashMap in <c>fileSystems</c> unchanged, so there is no patched class to look into; the message
    /// is the patch's own account of what it did.
    /// </exception>
    [Fact]
    public void PatchCreatesTheFileSystemsAsAConcurrentHashMap()
    {
        ClassPatchResult applied = UnionFileSystems.Patch.Apply(CreateClassFile(mapField: "fileSystems"));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.ReplacedConstructions);
        ClassFileAssert.Calls(patched, Constructor, ConcurrentHashMap, Constructor, descriptor: "()V");
        ClassFileAssert.Calls(patched, Constructor, HashMap, Constructor, descriptor: "()V");
    }

    /// <summary>
    /// In the real provider of each release the constructor creates the map of file systems as a ConcurrentHashMap and no HashMap any more, and nothing else in
    /// the jar changes.
    /// </summary>
    /// <param name="repository">The Maven repository.</param>
    /// <param name="coordinates">The artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <exception cref="InvalidOperationException">
    /// The one result that says a patch changed a class of the jar holds no bytes of the patched class; the message is that result's account of what the patch
    /// did.
    /// </exception>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task PatchCreatesTheFileSystemsOfARealProviderAsAConcurrentHashMap(string repository, string coordinates, string sha256)
    {
        string jar = await new MavenArtifact(repository, coordinates, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        ClassPatchResult applied = Assert.Single(PatcherAgent.Patches.SelectMany(patch => patch.ApplyToJar(jar)),
            static result => result.Changed);

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(ClassName, applied.ClassName);
        Assert.Equal(expected: 1, applied.ReplacedConstructions);
        ClassFileAssert.Calls(patched, Constructor, ConcurrentHashMap, Constructor, descriptor: "()V");
        ClassFileAssert.DoesNotCall(patched, Constructor, HashMap, Constructor, descriptor: "()V");
    }

    /// <summary>A constructor that stores its HashMap in another field isn't the code this fix was written for, so the class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesAConstructorThatDoesntCreateTheFileSystems()
    {
        ClassPatchResult skipped = UnionFileSystems.Patch.Apply(CreateClassFile(mapField: "providers"));

        Assert.False(skipped.Changed);
        Assert.Contains("no construction of " + HashMap, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>FancyModLoader 10.0.3 has no union file systems any more, and nothing in its securejarhandler changes.</summary>
    [Fact]
    public async Task PatchLeavesTheSecureJarHandlerWithoutUnionFileSystems()
    {
        string jar = await new MavenArtifact(NeoForged,
            coordinates: "net.neoforged.fancymodloader:securejarhandler:10.0.3",
            sha256: "1477820b254b3c6341fb0a015963cd1196065a3adff52e1be30dc65cf85e05fd").GetPathAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(PatcherAgent.Patches.SelectMany(patch => patch.ApplyToJar(jar)), static result => result.Changed);
    }

    // A constructor that stores a new HashMap in the given field and another in a field of its own, as the provider's stores its file systems.
    private static byte[] CreateClassFile(string mapField)
    {
        const string map = "Ljava/util/Map;";

        return new ClassFileWriter(ClassName).AddField(mapField, map)
            .AddField(name: "attributes", map)
            .AddMethod(Constructor,
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .InvokeSpecial(owner: "java/lang/Object", Constructor, descriptor: "()V")
                    .ALoad(slot: 0)
                    .New(HashMap)
                    .Dup()
                    .InvokeSpecial(HashMap, Constructor, descriptor: "()V")
                    .PutField(ClassName, mapField, map)
                    .ALoad(slot: 0)
                    .New(HashMap)
                    .Dup()
                    .InvokeSpecial(HashMap, Constructor, descriptor: "()V")
                    .PutField(ClassName, name: "attributes", map)
                    .Return(),
                accessFlags: 0x0001)
            .ToArray();
    }
}
