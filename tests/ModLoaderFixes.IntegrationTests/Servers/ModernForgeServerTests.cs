using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>Real Forge and NeoForge servers for Minecraft 1.13 to 1.20.2, as itzg installs them, with the agent loaded and players logging in through FML's handshake.</summary>
[Collection(ForgeServer.Collection)]
public sealed class ModernForgeServerTests
{
    private const string ConfigurationWatchPatched = "ModLoaderFixes: patched net/minecraftforge/fml/loading/FMLConfig (loadFrom(Ljava/nio/file/Path;)V: 1 invocation of com/electronwill/nightconfig/core/file/GenericBuilder.autosave removed; loadFrom(Ljava/nio/file/Path;)V: 1 invocation of com/electronwill/nightconfig/core/file/GenericBuilder.autoreload removed)";

    private const string ListenerChildrenPatched = "ModLoaderFixes: patched net/minecraftforge/eventbus/ListenerList$ListenerListInst (";
    private const string ReloadPatched = " (<init>: 1 invocation of com/google/common/collect/Sets.newHashSet redirected)";
    // The handshake's builder of reply handlers is NetworkInitialization in 1.13.2 and the handler from 1.14, so the line is matched without the class.
    private const string RepliesPatched = " (indexed replies handled before they are marked answered (1 change))";

    /// <summary>
    /// One build of each Forge branch whose handshake handler keeps <c>sentMessages</c> in an ArrayList, the ones seen dropping players among them, with the itzg image
    /// that runs it, the protocol version of its Minecraft and FML's network version. Forge from 1.17 runs on the Java 17 image, whose Ubuntu 20.04 has glibc 2.31, so
    /// these also load the agent on an older glibc than the runner that published it. Renovate updates the images through the custom manager in .github/renovate.json.
    /// </summary>
    public static TheoryData<string, string, string, string, int, string> Builds { get; } = new()
    {
        { "itzg/minecraft-server:2026.10.0-java8", "FORGE", "1.13.2", "25.0.223", 404, "FML2" },
        { "itzg/minecraft-server:2026.10.0-java8", "FORGE", "1.14.4", "28.2.26", 498, "FML2" },
        { "itzg/minecraft-server:2026.10.0-java8", "FORGE", "1.15", "29.0.4", 573, "FML2" },
        { "itzg/minecraft-server:2026.10.0-java8", "FORGE", "1.16.5", "36.2.39", 754, "FML2" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.17.1", "37.1.1", 756, "FML2" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.18.2", "40.2.0", 758, "FML3" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.19.2", "43.5.0", 760, "FML3" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.20.1", "47.4.28", 763, "FML3" },
        { "itzg/minecraft-server:2026.10.0-java17", "NEOFORGE", "1.20.1", "47.1.106", 763, "FML3" },
        { "itzg/minecraft-server:2026.10.0-java17", "NEOFORGE", "1.20.2", "20.2.93", 764, "FML3" },
    };

    /// <summary>
    /// The agent makes the handshake handler's list of sent messages a CopyOnWriteArrayList, and a burst of Forge clients all reach the play state. Each client
    /// acknowledges FML's messages on the network thread while the server thread sends the next ones, which is when Forge without the agent can throw
    /// ConcurrentModificationException and drop the player.
    /// </summary>
    /// <param name="image">The itzg image.</param>
    /// <param name="type">itzg's TYPE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="loaderVersion">The Forge or NeoForge build.</param>
    /// <param name="protocol">The protocol version of that Minecraft.</param>
    /// <param name="fmlVersion">FML's network version.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerKeepsPlayersThatLogInThroughFmlsHandshake(string image, string type, string version, string loaderVersion, int protocol, string fmlVersion)
    {
        ArgumentNullException.ThrowIfNull(version);

        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        string loginLoad = await ForgeServer.CompileLoginLoadAsync(timeout.Token);

        await using IContainer server = ForgeServer.Create(image, type, version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(type == "NEOFORGE" ? "NEOFORGE_VERSION" : "FORGE_VERSION", loaderVersion)
            .WithLoginLoad(loginLoad)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);

        ExecResult logins = await ForgeServer.LogInPlayersAsync(server, protocol, fmlVersion, timeout.Token);

        logs = await ForgeServer.ReadLogsAsync(server, timeout.Token);

        // NeoForge from 1.20.2 has a handler of its own, which keeps the indexes in a fastutil IntArrayList, and the object holders under its own package.
        bool isNeoForgesOwn = type == "NEOFORGE" && version != "1.20.1";

        ForgeServer.AssertLogged(
            logs,
            isNeoForgesOwn
                ? "ModLoaderFixes: patched net/neoforged/neoforge/network/HandshakeHandler (<init>: 1 new it/unimi/dsi/fastutil/ints/IntArrayList replaced by new modloaderfixes/HandshakeIndexes stored in sentMessages)"
                : "HandshakeHandler (<init>: 1 new java/util/ArrayList replaced by new java/util/concurrent/CopyOnWriteArrayList stored in sentMessages)"
        );
        ForgeServer.AssertNotLogged(logs, unexpected: "ConcurrentModificationException");
        ForgeServer.AssertNotLogged(logs, unexpected: "Badly compressed packet");
        // Each client's last acknowledgement is handled before its index is removed, so the server thread can't finish the handshake while the network thread
        // still needs the handler.
        ForgeServer.AssertLogged(logs, RepliesPatched);

        // Mod loading fills ObjectHolderRegistry's handlers on these servers too, from the same worker threads as on clients.
        if (!isNeoForgesOwn)
        {
            ForgeServer.AssertLogged(
                logs,
                expected: "ModLoaderFixes: patched net/minecraftforge/registries/ObjectHolderRegistry (<clinit>()V: 1 new java/util/HashSet() replaced by java/util/concurrent/ConcurrentHashMap.newKeySet() stored in objectHolders)"
            );
        }

        // Forge before 1.20 builds its fml.toml with autosave and autoreload, which each server here corrects on its first start; Forge 1.20.1 and NeoForge don't.
        if (type == "FORGE" && !version.StartsWith(value: "1.20", StringComparison.Ordinal))
            ForgeServer.AssertLogged(logs, ConfigurationWatchPatched);
        else
            ForgeServer.AssertNotLogged(logs, ConfigurationWatchPatched);

        // Forge before 1.16 ships ModLauncher 2 to 5, whose class hierarchy cache the transforming threads share; from 1.16 ModLauncher keeps it safe itself.
        if (type == "FORGE" && string.CompareOrdinal(version, strB: "1.16") < 0)
            ForgeServer.AssertLogged(logs, ForgeServer.ModLauncherPatched);
        else
            ForgeServer.AssertNotLogged(logs, ForgeServer.ModLauncherPatched);

        // Forge for 1.13.2 ships Minecraft's flush of the outbound queue, which two threads can run at once; Mojang's 1.14.4 polls it under the queue's monitor.
        if (version == "1.13.2")
            ForgeServer.AssertLogged(logs, ForgeServer.FlushPatched);
        else
            ForgeServer.AssertNotLogged(logs, ForgeServer.FlushPatched);

        // The startup reloads the data packs with the workers as the game executor from 1.16, through the reload whose set of listeners the agent locks: AsyncReloader
        // by MCP's name, which Forge gives it from 1.14, and SimpleReloadInstance by Mojang's from 1.17. Both keep the same code where only the server or main thread
        // uses the set, before 1.16 and after 1.18.2, and the lock changes nothing there; Minecraft 1.13.2 reloads otherwise.
        if (version == "1.13.2")
            ForgeServer.AssertNotLogged(logs, ReloadPatched);
        else if (string.CompareOrdinal(version, strB: "1.17") < 0)
            ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched net/minecraft/resources/AsyncReloader" + ReloadPatched);
        else
            ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched net/minecraft/server/packs/resources/SimpleReloadInstance" + ReloadPatched);

        // Forge before 1.16.4-35.1.37 ships an EventBus before 4.0.0, whose listener lists' children the mod-loading workers add to while another mod's
        // registration iterates them; the builds here from 1.16.5 ship EventBus 4.0.0 or later, which lock them.
        if (type == "FORGE" && string.CompareOrdinal(version, strB: "1.16.5") < 0)
            ForgeServer.AssertLogged(logs, ListenerChildrenPatched);
        else
            ForgeServer.AssertNotLogged(logs, ListenerChildrenPatched);

        // Forge up to 1.16.5 generates its spawn area through the region, by MCP's name, whose setter the agent makes remove the block entity of a block that
        // another block replaces. Minecraft 1.17 no longer lets a dungeon set its spawner on a chest, and Forge runs it under Mojang's names.
        if (type == "FORGE" && string.CompareOrdinal(version, strB: "1.17") < 0)
            ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched net/minecraft/world/gen/WorldGenRegion" + ForgeServer.GenerationPatched);
        else
            ForgeServer.AssertNotLogged(logs, ForgeServer.GenerationPatched);

        ForgeServer.AssertNotLogged(logs, unexpected: "Exception generating new chunk");

        // Every one reads its jars through the JDK's zip file system, mod jars from 1.13.2 and the whole module layer from 1.17, on Java 8's com.sun.nio.zipfs and
        // later releases' jdk.nio.zipfs, whose channel the agent opens again after an interrupt on Java 8 and makes uninterruptible on later releases, where Forge
        // 1.20.1 and NeoForge's SecureJarHandler cast it to the JDK's FileChannelImpl.
        ForgeServer.AssertLogged(logs, ForgeServer.ZipChannelPatched);
        ForgeServer.AssertNotLogged(logs, unexpected: "ClassCastException");

        // The client reports a login that skipped FML's handshake separately, so this also says every player went through it.
        Assert.True(logins.ExitCode is 0, logins.Stdout + logins.Stderr);
        Assert.Contains(expectedSubstring: "LOGINS {logged in=200}", logins.Stdout, StringComparison.Ordinal);
    }
}
