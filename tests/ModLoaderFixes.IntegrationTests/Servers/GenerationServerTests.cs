using System.IO.Compression;

using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// Real Minecraft 1.16.5 servers, as itzg installs them, that generate a world in which a spawner replaces a chest that already has its block entity, with the
/// agent loaded through its jar.
/// </summary>
[Collection(ForgeServer.Collection)]
public sealed class GenerationServerTests
{
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";
    private const string Packs = "/packs/";
    private const string Position = "-25 63 74";
    private const UnixFileModes Readable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;
    private const string Seed = "78";

    /// <summary>
    /// The servers that name the generation region differently, each with its loader's version and the name it gives the region: vanilla's obfuscated one, MCP's
    /// under Forge, and CraftBukkit's under Paper.
    /// </summary>
    public static TheoryData<string, string, string> Servers { get; } = new()
    {
        { "VANILLA", string.Empty, "aam" },
        { "FORGE", "36.2.39", "net/minecraft/world/gen/WorldGenRegion" },
        { "PAPER", string.Empty, "net/minecraft/server/v1_16_R3/RegionLimitedWorldAccess" },
    };

    /// <summary>
    /// The server's data pack turns two of every chunk's features into a loot chest on the surface, whose loot table creates its block entity, and into veins of
    /// spawners that replace chests, as a dungeon's spawner replaces an earlier dungeon's chest. The same seed generates the same world on every server, and in
    /// it the chest at <c>-25 63 74</c> is one that a vein reaches. Without the agent the spawner there keeps the chest's block entity: vanilla and Forge answer
    /// <c>data get block</c> with <c>minecraft:chest</c>, and Paper logs that Bukkit will attempt to fix the block when it reads it. With the agent the region
    /// removes the chest's block entity as it sets the spawner, so the block is a spawner with a spawner's block entity.
    /// </summary>
    /// <param name="type">itzg's TYPE.</param>
    /// <param name="loaderVersion">Forge's build, or nothing for a server without a loader of its own version.</param>
    /// <param name="region">The name of the generation region on that server.</param>
    [Theory]
    [MemberData(nameof(Servers))]
    public async Task ServerGeneratesASpawnerWithItsOwnBlockEntityWhereAChestStood(string type, string loaderVersion, string region)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);
        byte[] pack = Zip(Path.Combine(AppContext.BaseDirectory, path2: "Fixtures", path3: "ReplacedChest", path4: "replaced"));

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        // itzg copies the zipped packs of DATAPACKS into the world before the server creates it, so the world generates with them from its first chunk.
        await using IContainer server = ForgeServer.Create(Image, type, version: "1.16.5", memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(name: "FORGE_VERSION", loaderVersion)
            .WithEnvironment(name: "SEED", Seed)
            .WithEnvironment(name: "DATAPACKS", Packs)
            .WithResourceMapping(pack, Packs + "replaced.zip", fileMode: Readable)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);

        // Paper keeps fewer chunks around the spawn point loaded than the others, so the chunk is loaded for all of them, which takes the server a few ticks.
        ExecResult loaded = await server.ExecAsync(["rcon-cli", "forceload add -25 74"], timeout.Token);
        ExecResult block = await AskAsync(server, "execute if block " + Position + " minecraft:spawner", timeout.Token);
        ExecResult entity = await AskAsync(server, "data get block " + Position + " id", timeout.Token);

        logs = await ForgeServer.ReadLogsAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, "ModLoaderFixes: patched " + region + ForgeServer.GenerationPatched);
        Assert.True(loaded.ExitCode is 0, loaded.Stdout + loaded.Stderr);
        Assert.Contains(expectedSubstring: "Test passed", block.Stdout, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "\"minecraft:mob_spawner\"", entity.Stdout, StringComparison.Ordinal);
        ForgeServer.AssertNotLogged(logs, unexpected: "Bukkit will attempt to fix this");
        ForgeServer.AssertNotLogged(logs, unexpected: "Exception generating new chunk");
    }

    // Asks the server's remote console, again while it answers that the chunk isn't loaded yet.
    private static async Task<ExecResult> AskAsync(IContainer server, string command, CancellationToken cancellationToken)
    {
        ExecResult answer = await server.ExecAsync(["rcon-cli", command], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        for (int attempt = 0; attempt < 30 && answer.Stdout.Contains(value: "not loaded", StringComparison.Ordinal); attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds: 1), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            answer = await server.ExecAsync(["rcon-cli", command], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return answer;
    }

    // A data pack as one file, with its pack.mcmeta at the top and every path with the forward slashes that Minecraft reads them by.
    private static byte[] Zip(string directory)
    {
        using MemoryStream bytes = new();

        using (ZipArchive archive = new(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string file in Directory.EnumerateFiles(directory, searchPattern: "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string name = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, newChar: '/');

                using Stream entry = archive.CreateEntry(name).Open();

                using FileStream source = File.OpenRead(file);

                source.CopyTo(entry);
            }
        }

        return bytes.ToArray();
    }
}
