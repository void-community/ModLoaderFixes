using System.Runtime.CompilerServices;

using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>PaperSpigot and vanilla Minecraft 1.7 servers, as itzg installs them, with the agent loaded through its jar.</summary>
[Collection(ForgeServer.Collection)]
public sealed class MinecraftServerTests
{
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";

    /// <summary>
    /// The vanilla releases at the edges of the two protocols of Minecraft 1.7, with their protocol version and the obfuscated names of their channel initializer and
    /// their NetworkManager.
    /// </summary>
    public static TheoryData<string, int, string, string> VanillaReleases { get; } = new()
    {
        { "1.7.2", 4, "mt", "ef" },
        { "1.7.10", 5, "nd", "ej" },
    };

    /// <summary>
    /// A PaperSpigot 1.7.10 server ticks its connections through the agent's helper and lets every player of a burst of logins reach the play state. The tick only
    /// now and then sees a connection between its two reads, so <see cref="PendingConnectionTests"/> runs it at that moment.
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
    }

    /// <summary>
    /// A vanilla 1.7 server adds its new connections to the tick through the agent's helper, which finds its initializer by the release's names, and lets every
    /// player of a burst of logins reach the play state, with its NetworkManager's sends synchronized.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="protocol">Its protocol version.</param>
    /// <param name="initializer">The obfuscated name of its channel initializer.</param>
    /// <param name="manager">The obfuscated name of its NetworkManager.</param>
    [Theory]
    [MemberData(nameof(VanillaReleases))]
    public async Task VanillaServerKeepsPlayersThatLogInUnderLoad(string version, int protocol, string initializer, string manager)
    {
        string logs = await LogInPlayersAsync(type: "VANILLA", version, protocol, string.Empty);

        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched " + initializer + " (initChannel(Lio/netty/channel/Channel;)V: new connections added to the network tick once their channel is active (2 changes))"
        );
        ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched " + manager + " (outbound queue flushed by one thread at a time (1 change))");
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

        logs = await ForgeServer.ReadLogsAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        ForgeServer.AssertNotLogged(logs, unexpected: "Failed to handle packet");
        Assert.True(logins.ExitCode is 0, logins.Stdout + logins.Stderr);
        Assert.Contains(expectedSubstring: "LOGINS {logged in=200}", logins.Stdout, StringComparison.Ordinal);

        return logs;
    }
}
