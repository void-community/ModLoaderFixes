using System.IO.Compression;

using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Worlds;

using Xunit;

namespace ModLoaderFixes.Tests.Worlds;

/// <summary>Making Forge's synchronous chunk load return the chunk that its provider has after it, in Forge 1.7.2 to 1.9.</summary>
public sealed class ChunkLoadsPatchTests
{
    private const string Chunk = "net/minecraft/world/chunk/Chunk";
    private const string Descriptor = "(Lnet/minecraft/world/World;Lnet/minecraft/world/chunk/storage/AnvilChunkLoader;L" + Provider + ";II)L" + Chunk + ";";
    private const string Executor = "net/minecraftforge/common/chunkio/ChunkIOExecutor";
    private const string Forge = "https://maven.minecraftforge.net";
    private const string GetSkipQueue = "(Ljava/lang/Object;)Ljava/lang/Object;";
    private const string Moved = "modloaderfixes$syncChunkLoad";
    private const string Provider = "net/minecraft/world/gen/ChunkProviderServer";
    private const string Queue = "net/minecraftforge/common/util/AsynchronousExecutor";

    /// <summary>
    /// The Forge builds at the ends of the range and those that Forge 1.7.2, 1.7.10, 1.8 and 1.8.9 ended on, each with whether its synchronous load returns
    /// what the queue gives: none before 1.7.2-10.12.2.1123, which has no executor yet, every build from there to 1.9-12.16.0.1884, and none from
    /// 1.9-12.16.0.1885, whose load returns the chunk of its own task.
    /// </summary>
    public static TheoryData<string, string, string> Builds { get; } = new()
    {
        { "1.7.2-10.12.2.1122", "861fc866666a3a9b2872d921309bf53fd89850d3cbaa623aa52a401a5a0df2eb", "absent" },
        { "1.7.2-10.12.2.1123", "00f8bf59928332c758dce6b452694545474c1f8e611b6ad328ac704a318a0676", "faulty" },
        { "1.7.2-10.12.2.1161-mc172", "17628bcf403e312f8de275633528c171795fd0b5469ba9e496baf401a2cd1b7e", "faulty" },
        { "1.7.10-10.13.4.1614-1.7.10", "00d1ca02192c7efb87da95552ebf247021f1e1d642f86d0a03076314def11529", "faulty" },
        { "1.8-11.14.4.1577", "e6f89f6fa17535ac9721b8339aa5af370b3a95aa11d12ea228fbc69f44425b86", "faulty" },
        { "1.8.9-11.15.1.2318-1.8.9", "596512ad5f12f95d8a3170321543d4455d23b8fe649c68580c5f828fe74f6668", "faulty" },
        { "1.9-12.16.0.1884-1.9", "bb256aaa34c3a555d69278e73ec2945d0d95ac68f33e1b73bec7149ae6cc9cce", "faulty" },
        { "1.9-12.16.0.1885-1.9", "54a9c3bd43d477cc765c65a7b2a8453e530a3238150ebf66083e57f63905b0be", "repaired" },
    };

    /// <summary>An executor whose load returns the chunk of its own task, as Forge's from 1.9-12.16.0.1885, is left alone.</summary>
    [Fact]
    public void PatchLeavesAnExecutorThatReturnsItsTasksChunk()
    {
        byte[] classFile = new ClassFileWriter(Executor).AddMethod(name: "syncChunkLoad", Descriptor, static code => code.AConstNull().AReturn(), accessFlags: 0x0009).ToArray();

        Assert.False(ChunkLoads.Patch.Apply(classFile).Changed);
    }

    /// <summary>
    /// Under the names a running Forge gives the game's classes, syncChunkLoad runs its moved body and then returns the provider's loadChunk, the moved body
    /// still asks the queue, and a class patched once is left as it is.
    /// </summary>
    [Fact]
    public void PatchReturnsTheProvidersChunkUnderForgesNames()
    {
        ClassPatchResult result = ChunkLoads.Patch.Apply(CreateExecutor(Descriptor));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        ClassFileAssert.Calls(patched, method: "syncChunkLoad", Executor, Moved, Descriptor);
        ClassFileAssert.Calls(patched, method: "syncChunkLoad", Provider, name: "func_73158_c", "(II)L" + Chunk + ";");
        ClassFileAssert.DoesNotCall(patched, method: "syncChunkLoad", Queue, name: "getSkipQueue", GetSkipQueue);
        ClassFileAssert.Calls(patched, Moved, Queue, name: "getSkipQueue", GetSkipQueue);
        Assert.False(ChunkLoads.Patch.Apply(patched).Changed);
    }

    /// <summary>
    /// Each real Forge jar has the faulty load exactly in the builds of the range: there syncChunkLoad returns what the queue's getSkipQueue gives. A jar on disk
    /// still names the game's classes by their obfuscated names, which FML only replaces as it loads Forge, so the patch, which names them as a running Forge
    /// does, leaves every jar as it is; the servers show it applied.
    /// </summary>
    /// <param name="version">The Forge build.</param>
    /// <param name="sha256">The pinned checksum of its universal jar.</param>
    /// <param name="load">Whether the build has no executor, the faulty load or the repaired one.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task RealForgeHasTheFaultyLoadInTheBuildsOfTheRange(string version, string sha256, string load)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string jar = await new MavenArtifact(Forge, "net.minecraftforge:forge:" + version + ":universal", sha256).GetPathAsync(cancellationToken);
        byte[]? executor = await ReadClassAsync(jar, Executor, cancellationToken);
        JavaMethod getSkipQueue = JavaMethod.Virtual(Queue, name: "getSkipQueue", GetSkipQueue);
        JavaClassFile? model = executor is null ? null : JavaClassFile.Read(executor);
        ClassMember? syncChunkLoad = model?.Methods.SingleOrDefault(static method => method.Name == "syncChunkLoad");
        bool asksTheQueue = model is not null && syncChunkLoad?.Code is { } code && code.Instructions.Any(instruction => model.Invokes(instruction, getSkipQueue));
        string found = model is null ? "absent" : asksTheQueue ? "faulty" : "repaired";

        Assert.Equal(load, found);
        Assert.DoesNotContain(ChunkLoads.Patch.ApplyToJar(jar), static result => result.Changed);
    }

    // The load as far as the patch looks at it: it asks the queue for the chunk and returns what it gets.
    private static byte[] CreateExecutor(string descriptor)
    {
        return new ClassFileWriter(Executor)
            .AddField(name: "instance", "L" + Queue + ";", accessFlags: 0x0008)
            .AddMethod(
                name: "syncChunkLoad",
                descriptor,
                static code => code.GetStatic(Executor, name: "instance", "L" + Queue + ";")
                    .ALoad(slot: 0)
                    .Invoke(JavaMethod.Virtual(Queue, name: "getSkipQueue", GetSkipQueue))
                    .CheckCast(Chunk)
                    .AReturn(),
                accessFlags: 0x0009
            )
            .ToArray();
    }

    private static async Task<byte[]?> ReadClassAsync(string jar, string className, CancellationToken cancellationToken)
    {
        ZipArchive archive = await ZipFile.OpenReadAsync(jar, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        await using (archive.ConfigureAwait(continueOnCapturedContext: false))
        {
            if (archive.GetEntry(className + ".class") is not { } entry)
                return null;

            Stream stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            using MemoryStream bytes = new();

            await using (stream.ConfigureAwait(continueOnCapturedContext: false))
                await stream.CopyToAsync(bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            return bytes.ToArray();
        }
    }
}
