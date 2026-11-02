using ModLoaderFixes.Mappings;

using Xunit;

namespace ModLoaderFixes.Tests.Mappings;

/// <summary>The generator of <c>Mappings.g.cs</c>: its readers of each mapping format, its routes between them, and the names it resolves from the real mappings.</summary>
public sealed class MappingsTests
{
    private const string Manager = "net/minecraft/network/NetworkManager";

    /// <summary>Forge, Ornithe and Mojang all map 1.14.4, and their three routes to its NetworkManager agree, by MCP's names and by Mojang's.</summary>
    [Fact]
    public async Task ForgeOrnitheAndMojangAgreeOnMinecraft1144()
    {
        using HttpClient client = new();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        MappingSources sources = await MappingSources.CreateAsync(client, Cache(), cancellationToken);
        Resolver resolver = new(sources);
        ClassRequest srg = new(Property: "Manager", Manager, [new MethodName(Name: "func_179290_a", Descriptor: "(Lnet/minecraft/network/IPacket;)V")]);

        ClassRequest official = new(
            Property: "Connection",
            Name: "net/minecraft/network/Connection",
            [new MethodName(Name: "send", Descriptor: "(Lnet/minecraft/network/protocol/Packet;)V")]
        );

        Resolution byForge = await resolver.ResolveAsync(Namespace.Srg, srg, version: "1.14.4", cancellationToken);
        Resolution byMojang = await resolver.ResolveAsync(Namespace.Official, official, version: "1.14.4", cancellationToken);

        Assert.Equal(
            ["Forge's 1.14.4", "Forge's 1.14.3 to Ornithe's 1.14.3 to Ornithe's 1.14.4", "Forge's 1.15 to Mojang's 1.15 to Mojang's 1.14.4"],
            byForge.Routes
        );
        Assert.Equal(["Mojang's 1.14.4", "Mojang's 1.15 to Forge's 1.15 to Forge's 1.14.4"], byMojang.Routes);
        Assert.Equal(byForge.Naming.Key, byMojang.Naming.Key);
        Assert.Equal(expected: "jc a(Lkc;)V", byForge.Naming.Key);
    }

    /// <summary>A naming whose methods another class of the same obfuscated name also declares, in a release that names the requested class otherwise, is refused.</summary>
    [Fact]
    public async Task GeneratorRejectsANamingThatAnotherClassMatches()
    {
        FakeMappingSources sources = new FakeMappingSources(releases: ["1.0", "1.1"])
            .With(
                Namespace.Srg,
                version: "1.0",
                Mapping.ReadSrg(source: "Forge's 1.0", text: "CL: a net/Manager\nMD: a/c ()V net/Manager/func_1 ()V\n")
            )
            .With(
                Namespace.Srg,
                version: "1.1",
                Mapping.ReadSrg(
                    source: "Forge's 1.1",
                    text: "CL: d net/Manager\nCL: a net/Other\nMD: d/e ()V net/Manager/func_1 ()V\nMD: a/c ()V net/Other/func_2 ()V\n"
                )
            );

        RequestBlock block = new(
            From: "1.0",
            To: "1.1",
            Namespace.Srg,
            [new ClassRequest(Property: "Manager", Name: "net/Manager", [new MethodName(Name: "func_1", Descriptor: "()V")])]
        );

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Generator.ResolveAsync([block], sources, TestContext.Current.CancellationToken));

        Assert.Contains(expectedSubstring: "also matches net/Other in 1.1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A release that Forge never mapped, such as 1.7.5, gets its names through Ornithe from the nearest releases on either side that Forge did map.</summary>
    [Fact]
    public async Task GeneratorResolvesEveryVanilla17Release()
    {
        using HttpClient client = new();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        MappingSources sources = await MappingSources.CreateAsync(client, Cache(), cancellationToken);

        IReadOnlyList<RequestBlock> requests = RequestBlock.Parse(
            await File.ReadAllTextAsync(Path.Combine(Repository(), path2: "src", path3: "ModLoaderFixes", path4: "Mappings.txt"), cancellationToken)
        );

        IReadOnlyList<ResolvedClass> classes = await Generator.ResolveAsync(requests, sources, cancellationToken);
        Resolution resolution = await new Resolver(sources).ResolveAsync(Namespace.Srg, requests[index: 0].Classes[index: 0], version: "1.7.5", cancellationToken);

        Assert.Equal(
            ["Forge's 1.7.2 to Ornithe's 1.7.2 to Ornithe's 1.7.5", "Forge's 1.7.10 to Ornithe's 1.7.10 to Ornithe's 1.7.5"],
            resolution.Routes
        );
        Assert.Equal(
            [
                "1.7.2 to 1.7.4: ef a(Lfk;[Lio/netty/util/concurrent/GenericFutureListener;)V h()V",
                "1.7.5: eg a(Lfq;[Lio/netty/util/concurrent/GenericFutureListener;)V h()V",
                "1.7.6 and 1.7.7: ej a(Lft;[Lio/netty/util/concurrent/GenericFutureListener;)V h()V",
                "1.7.8 to 1.7.10: ej a(Lft;[Lio/netty/util/concurrent/GenericFutureListener;)V i()V",
            ],
            Namings(classes, Manager)
        );
        Assert.Equal(
            ["1.7.2: mt ", "1.7.3 and 1.7.4: mw ", "1.7.5 to 1.7.9: nc ", "1.7.10: nd "],
            Namings(classes, name: "net/minecraft/network/NetworkSystem$1")
        );
        Assert.Equal(
            ["1.7.2: mu ", "1.7.3 and 1.7.4: mx ", "1.7.5 to 1.7.9: nd ", "1.7.10: ne "],
            Namings(classes, name: "net/minecraft/network/NetworkSystem$2")
        );
    }

    /// <summary>Runs of consecutive releases are named by their ends, and two by both.</summary>
    [Fact]
    public void LabelNamesRunsOfReleases()
    {
        string[] releases = ["1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7"];

        Assert.Equal(expected: "1.0 to 1.2, 1.4, 1.6 and 1.7", Generator.Label(["1.0", "1.1", "1.2", "1.4", "1.6", "1.7"], releases));
    }

    /// <summary>Mojang's files name Java types, which become descriptors by the obfuscated names of the classes they mention, arrays and primitives included.</summary>
    [Fact]
    public void ProGuardMethodsGetObfuscatedDescriptors()
    {
        Mapping mapping = Mapping.ReadProGuard(
            source: "Mojang's 1.0",
            "# comment\nnet.minecraft.network.Connection -> wk:\n    1:2:void send(net.minecraft.network.protocol.Packet,int[]) -> a\n    void <init>() -> <init>\n"
                + "net.minecraft.network.protocol.Packet -> abc:\n"
        );

        Assert.Equal(
            new MethodName(Name: "a", Descriptor: "(Labc;[I)V"),
            mapping.ObfuscatedMethod(
                owner: "net/minecraft/network/Connection",
                new MethodName(Name: "send", Descriptor: "(Lnet/minecraft/network/protocol/Packet;[I)V")
            )
        );
        Assert.Equal(expected: "send", mapping.NamedMethod(owner: "wk", name: "a", descriptor: "(Labc;[I)V"));
    }

    /// <summary>A line that is neither a block, a class nor a method is refused with its number.</summary>
    [Fact]
    public void RequestsRejectALineOutsideABlock()
    {
        FormatException exception = Assert.Throws<FormatException>(static () => RequestBlock.Parse(text: "# comment\nNetworkManager net/minecraft/network/NetworkManager\n"));

        Assert.Contains(expectedSubstring: "Line 2", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A release without Forge's names gets them through Ornithe's intermediary names from the releases on either side that have both, methods and their
    /// descriptors included.
    /// </summary>
    [Fact]
    public async Task ResolverBridgesAReleaseThatForgeNeverMapped()
    {
        Resolution resolution = await new Resolver(Linked(ornithe10: "c b C_2\nc a C_1\n\tm\t(Lb;)V\tc\tm_1\n"))
            .ResolveAsync(Namespace.Srg, Request(), version: "1.1", TestContext.Current.CancellationToken);

        Assert.Equal(expected: "d f(Le;)V", resolution.Naming.Key);
        Assert.Equal(["Forge's 1.0 to Ornithe's 1.0 to Ornithe's 1.1", "Forge's 1.2 to Ornithe's 1.2 to Ornithe's 1.1"], resolution.Routes);
    }

    /// <summary>Routes that find the class under different names stop the generator rather than let it pick one.</summary>
    [Fact]
    public async Task ResolverRejectsMappingsThatDisagree()
    {
        FakeMappingSources sources = Linked(ornithe10: "c b C_2\nc a C_9\nc z C_1\n\tm\t(Lb;)V\tq\tm_1\n");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new Resolver(sources).ResolveAsync(Namespace.Srg, Request(), version: "1.0", TestContext.Current.CancellationToken));

        Assert.Contains(expectedSubstring: "The mappings disagree on net/Manager in 1.0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>MCPConfig's TSRG files name each class's methods under it, by their obfuscated descriptors.</summary>
    [Fact]
    public void TsrgMethodsGetTheirNames()
    {
        Mapping mapping = Mapping.ReadTsrg(
            source: "Forge's 1.14.4",
            text: "jc net/minecraft/network/NetworkManager\n\ta field_150740_a\n\ta (Lkc;)V func_179290_a\nkc net/minecraft/network/IPacket\n"
        );

        Assert.Equal(
            new MethodName(Name: "a", Descriptor: "(Lkc;)V"),
            mapping.ObfuscatedMethod(Manager, new MethodName(Name: "func_179290_a", Descriptor: "(Lnet/minecraft/network/IPacket;)V"))
        );
    }

    private static string Cache()
    {
        return Path.Combine(Repository(), path2: "artifacts", path3: "mappings");
    }

    // Releases 1.0 to 1.2, which Ornithe maps and Forge maps except 1.1, with net/Manager and its packet under other names in each.
    private static FakeMappingSources Linked(string ornithe10)
    {
        const string srg10 = "CL: a net/Manager\nCL: b net/Packet\nMD: a/c (Lb;)V net/Manager/func_1 (Lnet/Packet;)V\n";
        const string srg12 = "CL: x net/Manager\nCL: y net/Packet\nMD: x/z (Ly;)V net/Manager/func_1 (Lnet/Packet;)V\n";

        return new FakeMappingSources(releases: ["1.0", "1.1", "1.2"])
            .With(Namespace.Srg, version: "1.0", Mapping.ReadSrg(source: "Forge's 1.0", srg10))
            .With(Namespace.Srg, version: "1.2", Mapping.ReadSrg(source: "Forge's 1.2", srg12))
            .With(Namespace.Intermediary, version: "1.0", Mapping.ReadTiny(source: "Ornithe's 1.0", Tiny(ornithe10)))
            .With(
                Namespace.Intermediary,
                version: "1.1",
                Mapping.ReadTiny(source: "Ornithe's 1.1", Tiny(classes: "c e C_2\nc d C_1\n\tm\t(Le;)V\tf\tm_1\n"))
            )
            .With(
                Namespace.Intermediary,
                version: "1.2",
                Mapping.ReadTiny(source: "Ornithe's 1.2", Tiny(classes: "c y C_2\nc x C_1\n\tm\t(Ly;)V\tz\tm_1\n"))
            );
    }

    private static List<string> Namings(IReadOnlyList<ResolvedClass> classes, string name)
    {
        return [.. classes.Single(resolved => resolved.Request.Name == name).Namings.Select(static naming => naming.Versions + ": " + naming.Naming.Key)];
    }

    private static string Repository()
    {
        string directory = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(directory, path2: "ModLoaderFixes.slnx")))
            directory = Path.GetDirectoryName(directory) ?? throw new InvalidOperationException(message: "The tests run outside the repository.");

        return directory;
    }

    private static ClassRequest Request()
    {
        return new ClassRequest(Property: "Manager", Name: "net/Manager", [new MethodName(Name: "func_1", Descriptor: "(Lnet/Packet;)V")]);
    }

    // A Tiny v2 file of classes, each followed by its methods, written with spaces between a class line's parts for brevity.
    private static string Tiny(string classes)
    {
        return "tiny\t2\t0\tofficial\tintermediary\n" + string.Join(
            separator: '\n',
            classes.Split(separator: '\n').Select(static line => line.StartsWith(value: 'c') ? line.Replace(oldChar: ' ', newChar: '\t') : line)
        );
    }
}
