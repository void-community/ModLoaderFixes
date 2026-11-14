using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Worlds;

using Xunit;

namespace ModLoaderFixes.Tests.Worlds;

/// <summary>
/// Making the region that Minecraft 1.13 to 1.16.5 generates through remove a block's block entity when it puts another block in its place: by Forge's names
/// under each of the setter's three signatures, by each vanilla release's, and by Paper's in each revision.
/// </summary>
public sealed class GenerationBlockEntitiesPatchTests
{
    private const string Added = "modloaderfixes$setBlockState";
    private const string Chunk = "net/minecraft/world/chunk/IChunk";
    private const string Helper = "modloaderfixes/GenerationBlocks";
    private const string HelperDescriptor = "(Ljava/lang/Object;Ljava/lang/Object;Ljava/util/function/Function;Ljava/lang/Object;Ljava/lang/Object;Ljava/util/function/BiConsumer;)V";
    private const string Interface = "interface";
    private const string Position = "net/minecraft/util/math/BlockPos";
    private const string Region = "net/minecraft/world/gen/WorldGenRegion";

    /// <summary>
    /// Forge's region sets the block through the added method under each signature its setter has had: with 1.13.2's state, which is an interface, with the state
    /// of 1.14 to 1.15.2, and with 1.16's second number.
    /// </summary>
    /// <param name="setter">The setter's name under Forge.</param>
    /// <param name="state">The block state's name there.</param>
    /// <param name="flags">The numbers the setter takes after the state.</param>
    /// <param name="stateKind">Whether the block state is an interface or a class, which decides how the region calls its getBlock.</param>
    [Theory]
    [InlineData("func_180501_a", "net/minecraft/block/state/IBlockState", "I", "interface")]
    [InlineData("func_180501_a", "net/minecraft/block/BlockState", "I", "class")]
    [InlineData("func_241211_a_", "net/minecraft/block/BlockState", "II", "class")]
    public void ForgePatchSetsTheBlockThroughTheAddedMethod(string setter, string state, string flags, string stateKind)
    {
        Names names = new(
            Region,
            setter,
            flags,
            Chunk,
            Set: "func_177436_a",
            Remove: "func_177425_e",
            Position,
            state,
            Block: "func_177230_c",
            BlockClass: "net/minecraft/block/Block",
            stateKind == Interface
        );

        AssertPatched(names, GenerationBlockEntities.Forge);
    }

    /// <summary>
    /// Paper's region sets the block through the added method under the names of each revision: 1.13's chunk has a and d, 1.13.2's setType and d, and from 1.14
    /// setType and removeTileEntity, and the setter is a from 1.16.
    /// </summary>
    /// <param name="revision">CraftBukkit's revision, which names the package.</param>
    /// <param name="setter">The setter's name there.</param>
    /// <param name="flags">The numbers the setter takes after the state.</param>
    /// <param name="set">The name of the chunk's setter.</param>
    /// <param name="remove">The name of the chunk's removal of a block entity.</param>
    /// <param name="stateKind">Whether the block state is an interface, as up to 1.13.2, or a class.</param>
    [Theory]
    [InlineData("v1_13_R1", "setTypeAndData", "I", "a", "d", "interface")]
    [InlineData("v1_13_R2", "setTypeAndData", "I", "setType", "d", "interface")]
    [InlineData("v1_14_R1", "setTypeAndData", "I", "setType", "removeTileEntity", "class")]
    [InlineData("v1_15_R1", "setTypeAndData", "I", "setType", "removeTileEntity", "class")]
    [InlineData("v1_16_R1", "a", "II", "setType", "removeTileEntity", "class")]
    [InlineData("v1_16_R2", "a", "II", "setType", "removeTileEntity", "class")]
    [InlineData("v1_16_R3", "a", "II", "setType", "removeTileEntity", "class")]
    public void PaperPatchSetsTheBlockThroughTheAddedMethod(string revision, string setter, string flags, string set, string remove, string stateKind)
    {
        string package = "net/minecraft/server/" + revision + "/";

        Names names = new(
            package + "RegionLimitedWorldAccess",
            setter,
            flags,
            package + "IChunkAccess",
            set,
            remove,
            package + "BlockPosition",
            package + "IBlockData",
            Block: "getBlock",
            package + "Block",
            stateKind == Interface
        );

        AssertPatched(names, Assert.Single(GenerationBlockEntities.Paper, patch => patch.Matches(Create(names), hierarchy: null)));
    }

    /// <summary>A region that was patched already is left as it is.</summary>
    [Fact]
    public void PatchLeavesAPatchedRegion()
    {
        Names names = new(
            Region,
            Setter: "func_241211_a_",
            Flags: "II",
            Chunk,
            Set: "func_177436_a",
            Remove: "func_177425_e",
            Position,
            State: "net/minecraft/block/BlockState",
            Block: "func_177230_c",
            BlockClass: "net/minecraft/block/Block",
            StateIsInterface: false
        );

        byte[] patched = GenerationBlockEntities.Forge.Apply(Create(names)).Bytes ?? throw new InvalidOperationException();

        Assert.False(GenerationBlockEntities.Forge.Apply(patched).Changed);
    }

    /// <summary>
    /// A region whose setter doesn't remove a block entity through the chunk, as Minecraft 1.17's under Mojang's names doesn't call these methods, is left alone
    /// with a reason.
    /// </summary>
    [Fact]
    public void PatchLeavesARegionWhoseSetterCallsOtherMethods()
    {
        byte[] classFile = new ClassFileWriter(Region, majorVersion: 52)
            .AddMethod(
                name: "func_241211_a_",
                "(L" + Position + ";Lnet/minecraft/block/BlockState;II)Z",
                static code => code.IConst(value: 1).IReturn(),
                accessFlags: 0x0001
            )
            .ToArray();

        ClassPatchResult result = GenerationBlockEntities.Forge.Apply(classFile);

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "block entity of another block removed", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// In the real region of the vanilla servers at the ends of the range, the setter hands the chunk, the position and the state to the added method and no longer
    /// sets the block in the chunk itself; the added method does, and calls the helper. No other class of the server changes, and 1.17's region, which a dungeon
    /// can't put a spawner on a chest through, doesn't either.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves its server under.</param>
    /// <param name="sha256">The server's pinned checksum.</param>
    /// <param name="className">The region's name in that server, or null where the patches change nothing.</param>
    /// <param name="setter">The setter's descriptor there.</param>
    /// <param name="chunk">The chunk's name there.</param>
    [Theory]
    [InlineData(
        "1.13",
        "d0caafb8438ebd206f99930cfaecfa6c9a13dca0",
        "e76f3927904d331c969a2c437d5661ac02f24be86062dd1c607bfd4ebdc550b9",
        "th",
        "(Lej;Lbkt;I)Z",
        "bmo"
    )]
    [InlineData(
        "1.16.5",
        "1b557e7b033b583cd9f66746b7a9ab1ec1673ced",
        "58f329c7d2696526f948470aa6fd0b45545039b64cb75015e64c12194b373da6",
        "aam",
        "(Lfx;Lceh;II)Z",
        "cfw"
    )]
    [InlineData(
        "1.17",
        "0a269b5f2c5b93b1712d0f5dc43b6182b9ab254e",
        "7b390d8d9f6b5649b226d82686aec7f11bd9aa4430bb5cac9072ffd32f3c1f4b",
        null,
        null,
        null
    )]
    public async Task PatchSetsTheBlockThroughTheAddedMethodInARealServer(string version, string sha1, string sha256, string? className, string? setter, string? chunk)
    {
        string jar = await RealJarTests.VanillaServer(version, sha1, sha256).GetPathAsync(TestContext.Current.CancellationToken);
        ClassPatchResult[] changed = [.. GenerationBlockEntities.Patches.SelectMany(patch => patch.ApplyToJar(jar)).Where(static result => result.Changed)];

        if (className is null || setter is null || chunk is null)
        {
            Assert.Empty(changed);

            return;
        }

        ClassPatchResult result = Assert.Single(changed);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        string position = setter[2..setter.IndexOf(value: ';', StringComparison.Ordinal)];
        string state = setter[(position.Length + 4)..setter.LastIndexOf(value: ';')];
        string set = "(L" + position + ";L" + state + ";Z)L" + state + ";";
        string added = "(L" + chunk + ";" + set[1..];

        Assert.Equal(className, result.ClassName);
        Assert.Equal(expected: 1, CountCalls(patched, method: "a", setter, JavaMethod.Static(className, Added, added)));
        Assert.Equal(expected: 0, CountCalls(patched, method: "a", setter, JavaMethod.Interface(chunk, name: "a", set)));
        Assert.Equal(expected: 1, CountCalls(patched, Added, added, JavaMethod.Interface(chunk, name: "a", set)));
        Assert.Equal(expected: 1, CountCalls(patched, Added, added, JavaMethod.Static(Helper, name: "removeEntityOfOtherBlock", HelperDescriptor)));
    }

    /// <summary>Vanilla's region sets the block through the added method under the names of the first and the last release of the range.</summary>
    /// <param name="className">The region's obfuscated name.</param>
    /// <param name="flags">The numbers the setter takes after the state.</param>
    /// <param name="chunk">The chunk's obfuscated name.</param>
    /// <param name="position">The position's.</param>
    /// <param name="state">The block state's.</param>
    /// <param name="block">The name of the state's getBlock.</param>
    /// <param name="blockClass">The block's obfuscated name.</param>
    /// <param name="stateKind">Whether the block state is an interface, as in 1.13, or a class.</param>
    [Theory]
    [InlineData("th", "I", "bmo", "ej", "bkt", "c", "bcj", "interface")]
    [InlineData("aam", "II", "cfw", "fx", "ceh", "b", "buo", "class")]
    public void VanillaPatchSetsTheBlockThroughTheAddedMethod(string className, string flags, string chunk, string position, string state, string block, string blockClass, string stateKind)
    {
        Names names = new(className, Setter: "a", flags, chunk, Set: "a", Remove: "d", position, state, block, blockClass, stateKind == Interface);

        AssertPatched(
            names,
            Assert.Single(GenerationBlockEntities.Patches, patch => patch.Matches(Create(names), hierarchy: null) && patch.Apply(Create(names)).Changed)
        );
    }

    private static void AssertPatched(Names names, ClassPatch patch)
    {
        ClassPatchResult result = patch.Apply(Create(names));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        string added = "(L" + names.Chunk + ";" + names.SetDescriptor[1..];

        ClassFileAssert.Calls(patched, names.Setter, names.Region, Added, added);
        ClassFileAssert.DoesNotCall(patched, names.Setter, names.Chunk, names.Set, names.SetDescriptor);
        ClassFileAssert.Calls(patched, names.Setter, names.Chunk, names.Remove, "(L" + names.Position + ";)V");
        ClassFileAssert.Calls(patched, Added, names.Chunk, names.Set, names.SetDescriptor);
        ClassFileAssert.Calls(patched, Added, Helper, name: "removeEntityOfOtherBlock", HelperDescriptor);
    }

    // The real classes overload their obfuscated names, so the method is found by its descriptor too.
    private static int CountCalls(byte[] classFile, string method, string descriptor, JavaMethod called)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);

        return model.FindMethod(method, descriptor)?.Code?.Instructions.Count(instruction => model.Invokes(instruction, called)) ?? 0;
    }

    // The setter as far as the patch looks at it, without the branches of the real one, which a class file that can hold the patch's lambdas only allows with
    // stack map frames: chunk.setBlockState(position, state, false); state.getBlock(); chunk.removeTileEntity(position); return true.
    private static byte[] Create(Names names)
    {
        string chunkDescriptor = "L" + names.Chunk + ";";
        string blockDescriptor = "()L" + names.BlockClass + ";";
        JavaMethod set = JavaMethod.Interface(names.Chunk, names.Set, names.SetDescriptor);
        JavaMethod remove = JavaMethod.Interface(names.Chunk, names.Remove, "(L" + names.Position + ";)V");
        JavaMethod block = names.StateIsInterface ? JavaMethod.Interface(names.State, names.Block, blockDescriptor) : JavaMethod.Virtual(names.State, names.Block, blockDescriptor);

        return new ClassFileWriter(names.Region, majorVersion: 52)
            .AddField(name: "chunk", chunkDescriptor)
            .AddMethod(
                names.Setter,
                "(L" + names.Position + ";L" + names.State + ";" + names.Flags + ")Z",
                code => code.ALoad(slot: 0)
                    .GetField(names.Region, name: "chunk", chunkDescriptor)
                    .ALoad(slot: 1)
                    .ALoad(slot: 2)
                    .IConst(value: 0)
                    .Invoke(set)
                    .Pop()
                    .ALoad(slot: 2)
                    .Invoke(block)
                    .Pop()
                    .ALoad(slot: 0)
                    .GetField(names.Region, name: "chunk", chunkDescriptor)
                    .ALoad(slot: 1)
                    .Invoke(remove)
                    .IConst(value: 1)
                    .IReturn(),
                accessFlags: 0x0001
            )
            .ToArray();
    }

    // A region's names in one naming: its own and its setter's, the chunk's with its two methods, the position's, and the block state's with its getBlock.
    private sealed record Names(
        string Region,
        string Setter,
        string Flags,
        string Chunk,
        string Set,
        string Remove,
        string Position,
        string State,
        string Block,
        string BlockClass,
        bool StateIsInterface
    )
    {
        // The chunk's setter takes the position, the state and whether the block is moving, and returns the state it replaced.
        internal string SetDescriptor => "(L" + Position + ";L" + State + ";Z)L" + State + ";";
    }
}
