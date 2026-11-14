using System.Runtime.CompilerServices;

using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>PaperSpigot 1.7.10 and vanilla Minecraft 1.7 and 1.8 servers, as itzg installs them, with the agent loaded through its jar.</summary>
[Collection(ForgeServer.Collection)]
public sealed class MinecraftServerTests
{
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";

    /// <summary>
    /// The vanilla releases at the edges of the two protocols of Minecraft 1.7, and 1.8, the last that leaves Nagle's algorithm on, with their protocol version and
    /// the obfuscated names of their channel initializer, which only 1.7's tick needs changed, of their NetworkManager and of the chat component that its
    /// closeChannel takes.
    /// </summary>
    public static TheoryData<string, int, string?, string, string> VanillaReleases { get; } = new()
    {
        { "1.7.2", 4, "mt", "ef", "fa" },
        { "1.7.10", 5, "nd", "ej", "fj" },
        { "1.8", 47, null, "gr", "ho" },
    };

    /// <summary>
    /// A PaperSpigot 1.7.10 server ticks its connections through the agent's helper and lets every player of a burst of logins reach the play state. The tick only
    /// now and then sees a connection between its two reads, so <see cref="PendingConnectionTests"/> runs it at that moment. Every player it then kicks gets the
    /// reason and the end of its connection, through the helper of its own that PaperSpigot's relocated Netty takes.
    /// </summary>
    [Fact]
    public async Task PaperServerKeepsPlayersThatLogInUnderLoad()
    {
        // Without a terminal, PaperSpigot 1.7.10's console reader floods the server thread with empty commands, which starve its tick, with the agent or without.
        string logs = await LogInPlayersAsync(type: "PAPER", version: "1.7.10", protocol: 5, extraArguments: "--noconsole");

        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched net/minecraft/server/v1_7_R4/ServerConnection (c()V: 1 invocation of net/minecraft/server/v1_7_R4/NetworkManager.isConnected redirected; "
            + "c()V: 1 invocation of net/minecraft/server/v1_7_R4/NetworkManager.a redirected)"
        );
        ForgeServer.AssertLogged(
            logs,
            expected: "ModLoaderFixes: patched net/minecraft/server/v1_7_R4/NetworkManager (modloaderfixes$channel()Ljava/lang/Object;: getter of field m added)"
        );
        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched net/minecraft/server/v1_7_R4/NetworkManager (close(Lnet/minecraft/server/v1_7_R4/IChatBaseComponent;)V: "
            + "1 invocation of net/minecraft/util/io/netty/channel/Channel.close redirected)"
        );
    }

    /// <summary>
    /// A vanilla 1.7 server adds its new connections to the tick through the agent's helper, which finds its initializer by the release's names, and a vanilla 1.7 or
    /// 1.8 server lets every player of a burst of logins reach the play state, with its NetworkManager's sends synchronized. Every player it then kicks gets the
    /// reason and the end of its connection: its closeChannel closes the channel through the agent's helper, which sends what Nagle's algorithm holds back first.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="protocol">Its protocol version.</param>
    /// <param name="initializer">The obfuscated name of its channel initializer, or <see langword="null"/> for 1.8, whose tick needs no fix.</param>
    /// <param name="manager">The obfuscated name of its NetworkManager.</param>
    /// <param name="component">The obfuscated name of the chat component that its closeChannel takes.</param>
    [Theory]
    [MemberData(nameof(VanillaReleases))]
    public async Task VanillaServerKeepsPlayersThatLogInUnderLoad(string version, int protocol, string? initializer, string manager, string component)
    {
        const string initializerPatched = " (initChannel(Lio/netty/channel/Channel;)V: new connections added to the network tick once their channel is active (2 changes))";

        string logs = await LogInPlayersAsync(type: "VANILLA", version, protocol, string.Empty);

        if (initializer is null)
            ForgeServer.AssertNotLogged(logs, initializerPatched);
        else
            ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched " + initializer + initializerPatched);

        ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched " + manager + " (outbound queue flushed by one thread at a time (1 change))");
        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched " + manager + " (a(L" + component + ";)V: 1 invocation of io/netty/channel/Channel.close redirected)"
        );
    }

    private static async Task<string> LogInPlayersAsync(string type, string version, int protocol, string extraArguments)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        string loginLoad = await ForgeServer.CompileLoginLoadAsync(timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        IContainer server = ForgeServer.Create(Image, type, version, memory: "1G", option: "-javaagent:", agent)
            .WithEnvironment(name: "EXTRA_ARGS", extraArguments)
            .WithLoginLoad(loginLoad)
            .Build();

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string logs = await ForgeServer.StartAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);

        ExecResult logins = await ForgeServer.LogInPlayersAsync(server, protocol, fmlVersion: null, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
        ExecResult kicks = await ForgeServer.KickPlayersAsync(server, protocol, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        logs = await ForgeServer.ReadLogsAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        ForgeServer.AssertNotLogged(logs, unexpected: "Failed to handle packet");
        Assert.True(logins.ExitCode is 0, logins.Stdout + logins.Stderr);
        Assert.Contains(expectedSubstring: "LOGINS {logged in=200}", logins.Stdout, StringComparison.Ordinal);
        ForgeServer.AssertKicked(kicks);

        return logs;
    }
}
