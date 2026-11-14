using System.Text;

using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// Real Paper servers, as itzg installs them, whose world has a spawner with a chest's block entity when their timings history reads the state of every block
/// entity, with the agent loaded through its jar.
/// </summary>
[Collection(ForgeServer.Collection)]
public sealed class PaperBlockEntityServerTests
{
    // Three minutes of looking at the log every two seconds: the first timings history comes a minute after the server has started.
    private const int Attempts = 90;
    private const uint Minecraft = 1000;
    private const string Region = "/data/world/region/r.0.0.mca";
    private const string Replaced = "ModLoaderFixes gave the block its own block entity so that its state can be read";
    private const int Sends = 6;
    private const UnixFileModes Writable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.GroupRead | UnixFileModes.OtherRead;

    /// <summary>
    /// The Paper releases that stop on such a block, each with the itzg image that runs it, CraftBukkit's revision, which names the patched class, and the
    /// system properties it needs: 1.14.3, whose last build has no repair of such a block entity, as every build from 1.14 to 1.14.4's build 216, and 1.17,
    /// whose repair replaces nothing. Paper 1.17 asks for Java 16, which itzg has no current image of, and runs on 17 when told not to check.
    /// </summary>
    public static TheoryData<string, string, string, string> Servers { get; } = new()
    {
        { "itzg/minecraft-server:2026.10.0-java8", "1.14.3", "v1_14_R1", string.Empty },
        { "itzg/minecraft-server:2026.10.0-java17", "1.17", "v1_17_R1", "Paper.IgnoreJavaVersion=true" },
    };

    /// <summary>
    /// The server places a spawner in a chunk it keeps loaded and stops; the test gives the spawner's block entity a chest's id in the region file, which is how
    /// the block lies on disk after world generation set a dungeon's spawner on an earlier dungeon's chest. Started again, the server reads the block's state when
    /// its first timings history is due, a minute later: without the agent that read stops it with a ClassCastException, and with it the spawner gets its own
    /// block entity, the log says so, and the server runs on.
    /// </summary>
    /// <param name="image">The itzg image.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="revision">CraftBukkit's revision of that version.</param>
    /// <param name="properties">The system properties the server needs, as itzg's JVM_DD_OPTS takes them.</param>
    [Theory]
    [MemberData(nameof(Servers))]
    public async Task ServerReadsTheStateOfASpawnerThatHasAChestsBlockEntity(string image, string version, string revision, string properties)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        // Timings keeps a history every five minutes unless told otherwise, and a minute is the least it accepts. The console's pipe takes the commands: the
        // remote console of these releases now and then starts without ever accepting a connection.
        await using IContainer server = ForgeServer.Create(image, type: "PAPER", version, memory: "1G", option: "-javaagent:", agent)
            .WithEnvironment(name: "LEVEL_TYPE", value: "FLAT")
            .WithEnvironment(name: "CREATE_CONSOLE_IN_PIPE", value: "true")
            .WithEnvironment(name: "JVM_DD_OPTS", properties)
            .WithResourceMapping(Encoding.UTF8.GetBytes(s: "timings:\n  history-interval: 60\n"), filePath: "/data/paper.yml", Minecraft, Minecraft, Writable)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);

        Task<long> stopping = server.GetExitCodeAsync(timeout.Token);

        logs = await SendAsync(server, stopping, command: "forceload add 0 0", effect: "to be force loaded", timeout.Token);
        logs = await SendAsync(server, stopping, command: "setblock 8 10 8 minecraft:spawner", effect: "Changed the block at 8, 10, 8", timeout.Token);
        logs = await SendAsync(server, stopping, command: "stop", effect: "Stopping server", timeout.Token);

        logs = await WaitAsync(server, stopping, static log => false, Attempts, timeout.Token);

        Assert.True(stopping.IsCompleted, "The server didn't stop. Its log:" + Environment.NewLine + logs);

        long stopped = await server.GetExitCodeAsync(timeout.Token);
        byte[] region = await server.ReadFileAsync(Region, timeout.Token);
        int swapped = RegionFiles.GiveSpawnersAChestsBlockEntity(region);

        await server.CopyAsync(region, Region, Minecraft, Minecraft, Writable, timeout.Token);
        await server.StartAsync(timeout.Token);

        Task<long> exit = server.GetExitCodeAsync(timeout.Token);

        logs = await WaitAsync(server, exit, static log => log.Contains(Replaced, StringComparison.Ordinal), Attempts, timeout.Token);
        logs = await SendAsync(server, exit, command: "data get block 8 10 8", effect: "8, 10, 8 has the following block data", timeout.Token);

        Assert.Equal(expected: 0, stopped);
        Assert.Equal(expected: 1, swapped);
        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched org/bukkit/craftbukkit/" + revision + "/block/CraftBlockEntityState (<init>(Lorg/bukkit/block/Block;Ljava/lang/Class;)V: 1 invocation of java/lang/Class.cast redirected)"
        );

        // Paper 1.14 also generates its world through the region whose setter the agent changes; Paper 1.17's dungeons can't set a spawner on a chest.
        if (revision == "v1_14_R1")
            ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched net/minecraft/server/v1_14_R1/RegionLimitedWorldAccess" + ForgeServer.GenerationPatched);
        else
            ForgeServer.AssertNotLogged(logs, ForgeServer.GenerationPatched);

        ForgeServer.AssertLogged(logs, expected: "Block at 8, 10, 8 is Block{minecraft:spawner} but has ");
        ForgeServer.AssertLogged(logs, Replaced);
        ForgeServer.AssertNotLogged(logs, unexpected: "ClassCastException");
        ForgeServer.AssertLogged(logs, expected: "id: \"minecraft:mob_spawner\"");
        Assert.False(exit.IsCompleted, ForgeServer.Printable(logs));
    }

    // Sends a command through the console's pipe until the server's log shows its effect. The pipe's reader closes it after each command, and a command
    // written in that moment is lost; one that arrives twice changes nothing the second time.
    private static async Task<string> SendAsync(IContainer server, Task<long> exit, string command, string effect, CancellationToken cancellationToken)
    {
        string logs = string.Empty;

        for (int attempt = 0; attempt < Sends && !logs.Contains(effect, StringComparison.Ordinal) && !exit.IsCompleted; attempt++)
        {
            // The pipe belongs to the user that itzg runs the server as, and its script refuses any other.
            ExecResult sent = await server.ExecAsync(["gosu", "1000:1000", "mc-send-to-console", command], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Assert.True(sent.ExitCode is 0, sent.Stdout + sent.Stderr);

            logs = await WaitAsync(server, exit, log => log.Contains(effect, StringComparison.Ordinal), attempts: 5, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return logs;
    }

    // Looks at the server's log, which holds both of its starts, every two seconds until it satisfies the condition, the server has exited or the attempts are
    // used up, and returns the log either way: the assertions say what is missing from it.
    private static async Task<string> WaitAsync(IContainer server, Task<long> exit, Func<string, bool> condition, int attempts, CancellationToken cancellationToken)
    {
        string logs = await ForgeServer.ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        for (int attempt = 0; attempt < attempts && !condition(logs) && !exit.IsCompleted; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds: 2), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            logs = await ForgeServer.ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return logs;
    }
}
