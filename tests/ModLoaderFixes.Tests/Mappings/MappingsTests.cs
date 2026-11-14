using System.Xml.Linq;

using ModLoaderFixes.Mappings;

using Xunit;

namespace ModLoaderFixes.Tests.Mappings;

/// <summary>The generator of <c>Mappings.g.cs</c>: its readers of each mapping format, its routes between them, and the names it resolves from the real mappings.</summary>
public sealed class MappingsTests
{
    private const string Manager = "net/minecraft/network/NetworkManager";

    // Releases on either side of Forge's change from MCP's names to Mojang's, and one that Mojang ships unobfuscated.
    private static readonly string[] NamedReleases = ["1.7.10", "1.16.5", "1.17", "26.1"];

    // The attributes of a MappedClass item, in the order the build writes them out.
    private static readonly string[] ItemFields = ["Include", "Class", "From", "To", "Methods"];

    /// <summary>Forge, Ornithe and Mojang all map 1.14.4, and their three routes to its NetworkManager agree, by MCP's names and by Mojang's.</summary>
    [Fact]
    public async Task ForgeOrnitheAndMojangAgreeOnMinecraft1144()
    {
        using HttpClient client = new();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        MappingSources sources = await MappingSources.CreateAsync(client, Cache(), cancellationToken);
        Resolver resolver = new(sources);

        ClassRequest srg = new(
            Property: "Manager",
            Manager,
            From: "1.14.4",
            To: "1.14.4",
            [new MethodName(Name: "func_179290_a", Descriptor: "(Lnet/minecraft/network/IPacket;)V")]
        );

        ClassRequest official = new(
            Property: "Connection",
            Name: "net/minecraft/network/Connection",
            From: "1.14.4",
            To: "1.14.4",
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

    /// <summary>
    /// The agent's MappedClass items resolve in every release they cover; a release that Forge never mapped, such as 1.7.5, gets its names through Ornithe from the
    /// nearest release that Forge did map.
    /// </summary>
    [Fact]
    public async Task GeneratorResolvesTheClassesOfTheProject()
    {
        using HttpClient client = new();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        MappingSources sources = await MappingSources.CreateAsync(client, Cache(), cancellationToken);

        IReadOnlyList<ClassRequest> requests = ClassRequest.Parse(ProjectRequests());
        IReadOnlyList<ResolvedClass> classes = await Generator.ResolveAsync(requests, sources, cancellationToken);
        ClassRequest manager = requests.Single(static request => request.Property == "NetworkManager");
        Resolution resolution = await new Resolver(sources).ResolveAsync(Namespace.Srg, manager, version: "1.7.5", cancellationToken);

        Assert.Equal(["Forge's 1.7.2 to Ornithe's 1.7.2 to Ornithe's 1.7.5"], resolution.Routes);
        Assert.Equal(
            [
                "1.7.2 to 1.7.4: ef h()V",
                "1.7.5: eg h()V",
                "1.7.6 and 1.7.7: ej h()V",
                "1.7.8 to 1.7.10: ej i()V",
                "1.8 and 1.8.1: gr m()V",
                "1.8.2 and 1.8.3: el m()V",
                "1.8.4 to 1.9.2: ek m()V",
                "1.9.3 and 1.9.4: em m()V",
                "1.10 to 1.10.2: eo m()V",
                "1.11 to 1.11.2: er m()V",
                "1.12 to 1.12.2: gw m()V",
                "1.13 to 1.13.2: hw o()V",
                "1.14 to 1.14.2: ja o()V",
                "1.14.3: jb o()V",
            ],
            Namings(classes, property: "NetworkManager")
        );
        // A second item names the same class for its closeChannel, whose descriptor carries the chat component's name, which changes more often than the class's.
        Assert.Equal(
            ["1.7.2 to 1.7.4: ef a(Lfa;)V", "1.7.5: eg a(Lfg;)V", "1.7.6 to 1.7.10: ej a(Lfj;)V", "1.8: gr a(Lho;)V"],
            Namings(classes, property: "ClosingNetworkManager")
        );
        Assert.Equal(["1.7.2: mt ", "1.7.3 and 1.7.4: mw ", "1.7.5 to 1.7.9: nc ", "1.7.10: nd "], Namings(classes, property: "NetworkInitializer"));
        Assert.Equal(["1.7.2: mu ", "1.7.3 and 1.7.4: mx ", "1.7.5 to 1.7.9: nd ", "1.7.10: ne "], Namings(classes, property: "MemoryInitializer"));
        // The region that 1.16 generates through, whose setter takes the position and the block state, and the base of block states, a nested class whose
        // getBlock the region calls on the state.
        Assert.Equal(
            ["1.16 and 1.16.1: zj a(Lfu;Lcfj;II)Z", "1.16.2 and 1.16.3: aam a(Lfx;Lcee;II)Z", "1.16.4 and 1.16.5: aam a(Lfx;Lceh;II)Z"],
            Namings(classes, property: "GenerationRegion16")
        );
        Assert.Equal(
            ["1.16 and 1.16.1: cfi$a b()Lbvr;", "1.16.2 and 1.16.3: ced$a b()Lbul;", "1.16.4 and 1.16.5: ceg$a b()Lbuo;"],
            Namings(classes, property: "BlockState16")
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

    /// <summary>A request that lacks one of its fields is refused with its number.</summary>
    [Fact]
    public void RequestsRejectALineWithoutAllItsFields()
    {
        FormatException exception = Assert.Throws<FormatException>(static () => ClassRequest.Parse(text: "Manager\tnet/Manager\t1.0\t1.1\t\nManager\tnet/Manager\t1.0\t\n"));

        Assert.Contains(expectedSubstring: "Request 2", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A request takes the names Forge runs its releases under, MCP's up to 1.16.5 and Mojang's from 1.17, and may not cross from one to the other.</summary>
    [Fact]
    public async Task RequestsTakeForgesNamesOfTheirReleases()
    {
        FakeMappingSources sources = new(releases: ["1.16.5", "1.17"]);
        ClassRequest request = new(Property: "Manager", Name: "net/Manager", From: "1.16.5", To: "1.17", []);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Generator.ResolveAsync([request], sources, TestContext.Current.CancellationToken));

        Assert.Equal([Namespace.Srg, Namespace.Srg, Namespace.Official, Namespace.Official], NamedReleases.Select(ClassRequest.NamesOf));
        Assert.Contains(expectedSubstring: "across Forge's change of names in 1.17", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A release without Forge's names gets them through Ornithe's intermediary names from the nearest release that has both, the earlier of two as near, methods
    /// and their descriptors included.
    /// </summary>
    [Fact]
    public async Task ResolverBridgesAReleaseThatForgeNeverMapped()
    {
        Resolution resolution = await new Resolver(Linked(ornithe10: "c b C_2\nc a C_1\n\tm\t(Lb;)V\tc\tm_1\n"))
            .ResolveAsync(Namespace.Srg, Request(), version: "1.1", TestContext.Current.CancellationToken);

        Assert.Equal(expected: "d f(Le;)V", resolution.Naming.Key);
        Assert.Equal(["Forge's 1.0 to Ornithe's 1.0 to Ornithe's 1.1"], resolution.Routes);
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

    // The namings of one MappedClass item, which its property names; two items may name the same class.
    private static List<string> Namings(IReadOnlyList<ResolvedClass> classes, string property)
    {
        return [.. classes.Single(resolved => resolved.Request.Property == property).Namings.Select(static naming => naming.Versions + ": " + naming.Naming.Key)];
    }

    // The agent's MappedClass items as its build writes them out for the generator.
    private static string ProjectRequests()
    {
        XDocument project = XDocument.Load(Path.Combine(Repository(), path2: "src", path3: "ModLoaderFixes", path4: "ModLoaderFixes.csproj"));

        IEnumerable<string> lines = project.Descendants(name: "MappedClass")
            .Select(static item => string.Join(separator: '\t', ItemFields.Select(name => (string?)item.Attribute(name) ?? "")));

        return string.Join(separator: '\n', lines);
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
        return new ClassRequest(
            Property: "Manager",
            Name: "net/Manager",
            From: "1.0",
            To: "1.2",
            [new MethodName(Name: "func_1", Descriptor: "(Lnet/Packet;)V")]
        );
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
