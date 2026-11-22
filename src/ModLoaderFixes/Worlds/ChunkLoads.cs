using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Worlds;

/// <summary>
/// Makes Forge's synchronous chunk load return the chunk it loaded or generated. From Forge 1.7.2-10.12.2.1123 to 1.9-12.16.0.1884,
/// <c>ChunkProviderServer.loadChunk</c> asks <c>AnvilChunkLoader.chunkExists</c>, which says yes for a chunk that waits to be saved, and then has
/// <c>ChunkIOExecutor.syncChunkLoad</c> read it. Minecraft's File IO thread takes a chunk off the list of those that wait before it writes it, so a chunk that
/// was never on disk is in neither place for a moment, and the read finds nothing. Forge then loads or generates the chunk through the provider, as its
/// comment says, but <c>syncChunkLoad</c> still returns what the read gave, null. The world's tick takes that for the chunk, and the server stops with
/// <c>Exception ticking world</c> and a NullPointerException in <c>World.func_147467_a</c>. It happens where chunks are saved for the first time and loaded
/// again at once, as when players leave a new world and others join right after. Forge 1.9-12.16.0.1885 returns the generated chunk. The patch moves the
/// body of <c>syncChunkLoad</c> into a method of its own and gives it one that runs that and then returns the provider's <c>loadChunk(x, z)</c>, which
/// looks the chunk up among the loaded ones first: it is the chunk that was read, or the one Forge put in its place.
/// </summary>
internal static class ChunkLoads
{
    private const int Static = 0x0008;

    // A chunk as a descriptor names it, and Forge's executor of chunk loads.
    private const string Chunk = "Lnet/minecraft/world/chunk/Chunk;";
    private const string ClassName = "net/minecraftforge/common/chunkio/ChunkIOExecutor";

    // What the log says the patch did.
    private const string Description = "synchronous chunk loads return the chunk the provider has";

    // syncChunkLoad: its descriptor, the method that keeps its body, and its name.
    private const string Descriptor = "(Lnet/minecraft/world/World;Lnet/minecraft/world/chunk/storage/AnvilChunkLoader;L" + Provider + ";II)" + Chunk;
    private const string MovedBody = "modloaderfixes$syncChunkLoad";
    private const string Name = "syncChunkLoad";

    // The server's chunk provider, which holds the chunk that a load generated.
    private const string Provider = "net/minecraft/world/gen/ChunkProviderServer";

    // The executor's load of one queued chunk at once, whose result syncChunkLoad returns as it is up to Forge 1.9-12.16.0.1884.
    private static readonly JavaMethod GetSkipQueue = JavaMethod.Virtual(owner: "net/minecraftforge/common/util/AsynchronousExecutor",
        name: "getSkipQueue",
        descriptor: "(Ljava/lang/Object;)Ljava/lang/Object;");

    // ChunkProviderServer.loadChunk, by the name FML gives it, which returns a chunk that is loaded without reading or generating anything.
    private static readonly JavaMethod LoadChunk = JavaMethod.Virtual(Provider, name: "func_73158_c", "(II)" + Chunk);
    private static readonly JavaMethod Moved = JavaMethod.Static(ClassName, MovedBody, Descriptor);

    /// <summary>
    /// Gets the patch of Forge's <c>ChunkIOExecutor</c>. It filters by name, so that the class of Forge from 1.9-12.16.0.1885 to 1.12.2, which returns the
    /// generated chunk itself, is skipped quietly.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.ForMatching(static name => name == ClassName, ClassName + " of Forge 1.7.2 to 1.9")
        .Edit(Description, static context => ReturnLoaded(context.Editor));

    // syncChunkLoad is the static method of that name which returns what the executor's getSkipQueue gives, with the descriptor that a running Forge gives
    // it once FML has deobfuscated the game's names in Forge's own classes. A class without it, as Forge's from 1.9-12.16.0.1885 or a Forge jar on disk,
    // which still has the obfuscated names, or one patched already, is left alone.
    private static int ReturnLoaded(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;

        bool isFaulty = model.FindMethod(Name, Descriptor) is { Code: { } code } load
            && (load.AccessFlags & Static) != 0
            && code.Instructions.Any(instruction => model.Invokes(instruction, GetSkipQueue));

        if (!isFaulty || model.FindMethod(MovedBody, Descriptor) is not null)
            return 0;

        // The fallback's name changed between builds, and loadChunk has been there in all of them.
        editor.ReplaceMethodBody(Name,
            Descriptor,
            MovedBody,
            static code => code.ALoad(slot: 0)
                .ALoad(slot: 1)
                .ALoad(slot: 2)
                .ILoad(slot: 3)
                .ILoad(slot: 4)
                .Invoke(Moved)
                .Pop()
                .ALoad(slot: 2)
                .ILoad(slot: 3)
                .ILoad(slot: 4)
                .Invoke(LoadChunk)
                .AReturn());

        return 1;
    }
}
