using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Worlds;

/// <summary>
/// Makes world generation in Minecraft 1.13 to 1.16.5 remove a block's block entity when it puts another block in its place. <c>WorldGenRegion.setBlockState</c>
/// sets the block in the chunk and, for a block with a block entity, only notes that the chunk has to create one; it removes the old block's entity only when the
/// new block has none. A dungeon keeps the chests it finds and sets its spawner at its centre whatever is there, so one whose centre lands on an earlier
/// dungeon's chest, which the dungeon has already given a loot table, leaves a spawner block with the chest's block entity. The dungeon logs
/// <c>Failed to fetch mob spawner entity</c>, the spawner never spawns, and Paper 1.14 to 1.14.4 before build 217 and Paper 1.17 stop with a
/// <c>ClassCastException</c> when their timings history reads the block. Minecraft 1.17 no longer lets a dungeon replace a chest. The patch adds a method to
/// the region that sets the block in the chunk, as the region did, and hands the replaced and the placed state to <c>modloaderfixes.GenerationBlocks</c>, which
/// removes the chunk's block entity at the position when their blocks differ, as a chunk of the world does itself; the region's call of the chunk goes to that
/// method. The same block in another state keeps its block entity, as a chest that a structure turns after it has filled it does.
/// </summary>
internal static class GenerationBlockEntities
{
    private const string Added = "modloaderfixes$setBlockState";
    private const string Block = "func_177230_c";
    private const string BlockDescriptor = "()Lnet/minecraft/block/Block;";
    private const string Description = "block entity of another block removed where generation replaces the block";
    private const string Position = "Lnet/minecraft/util/math/BlockPos;";
    private const int PrivateStaticSynthetic = 0x100A;
    private const string Remove = "func_177425_e";
    private const string RemoveDescriptor = "(" + Position + ")V";
    private const string Set = "func_177436_a";
    private const string State13 = "Lnet/minecraft/block/state/IBlockState;";
    private const string State14 = "Lnet/minecraft/block/BlockState;";

    private static readonly JavaMethod Accept = JavaMethod.Interface(owner: "java/util/function/BiConsumer", name: "accept", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)V");

    private static readonly JavaMethod Apply = JavaMethod.Interface(owner: "java/util/function/Function", name: "apply", descriptor: "(Ljava/lang/Object;)Ljava/lang/Object;");

    // The block state's getBlock under Forge's names and each vanilla release's. 1.16 declares it in the state's superclass and still calls it on the state.
    private static readonly JavaMethod[] Blocks =
    [
        .. new[] { MappedClasses.BlockState13, MappedClasses.BlockState14, MappedClasses.BlockState16 }.SelectMany(Namings).Select(static names => names.Virtual(Block, BlockDescriptor)),
    ];

    // The chunk's setBlockState and removeTileEntity under Forge's names and under each vanilla release's; a region calls the pair of its own release.
    private static readonly ChunkCalls[] Chunks =
    [
        .. Namings(MappedClasses.GenerationChunk13).Select(static names => new ChunkCalls(names.Interface(Set, SetDescriptor(State13)), names.Interface(Remove, RemoveDescriptor))),
        .. Namings(MappedClasses.GenerationChunk14).Select(static names => new ChunkCalls(names.Interface(Set, SetDescriptor(State14)), names.Interface(Remove, RemoveDescriptor))),
    ];

    // The region's setBlockState, which takes a state of another name in 1.13 and a second number from 1.16.
    private static readonly Region[] Regions =
    [
        new(MappedClasses.GenerationRegion13, Setter: "func_180501_a", "(" + Position + State13 + "I)Z"),
        new(MappedClasses.GenerationRegion14, Setter: "func_180501_a", "(" + Position + State14 + "I)Z"),
        new(MappedClasses.GenerationRegion16, Setter: "func_241211_a_", "(" + Position + State14 + "II)Z"),
    ];

    private static readonly JavaMethod RemoveEntityOfOtherBlock = JavaMethod.Static(
        owner: "modloaderfixes/GenerationBlocks",
        name: "removeEntityOfOtherBlock",
        descriptor: "(Ljava/lang/Object;Ljava/lang/Object;Ljava/util/function/Function;Ljava/lang/Object;Ljava/lang/Object;Ljava/util/function/BiConsumer;)V"
    );

    /// <summary>
    /// Gets the patch of Forge's region, by MCP's name <c>WorldGenRegion</c>, whose setter has one of three signatures from 1.13.2 to 1.16.5, and so one patch
    /// looks for each.
    /// </summary>
    internal static ClassPatch Forge { get; } = ClassPatch.For(MappedClasses.GenerationRegion13.Forge.Name)
        .Edit(
            Description,
            static context => RemoveStale(
                context.Editor,
                [.. Regions.Select(static region => region.Mapped.Forge.Virtual(region.Setter, region.Descriptor))],
                Chunks,
                Blocks
            )
        );

    /// <summary>
    /// Gets the patches of Paper's region, <c>RegionLimitedWorldAccess</c> in a package of each revision from 1.13 to 1.16.5, by the names that CraftBukkit and
    /// Paper give its setter and the chunk's methods there.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Paper { get; } =
    [
        .. new[] { "v1_13_R1", "v1_13_R2", "v1_14_R1", "v1_15_R1", "v1_16_R1", "v1_16_R2", "v1_16_R3" }.Select(static revision => PaperPatch("net/minecraft/server/" + revision + "/")),
    ];

    /// <summary>Gets the patches of the region: Forge's, vanilla's under each release's names from 1.13 to 1.16.5, and Paper's.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        Forge,
        .. Regions.SelectMany(static region => region.Mapped.Vanilla.Select(names => VanillaPatch(names, names.Virtual(region.Setter, region.Descriptor)))),
        .. Paper,
    ];

    private static bool Calls(JavaClassFile model, IReadOnlyList<Instruction> code, JavaMethod method)
    {
        return code.Any(instruction => model.Invokes(instruction, method));
    }

    private static IEnumerable<ClassNames> Namings(MappedClass mapped)
    {
        return [mapped.Forge, .. mapped.Vanilla];
    }

    // The region of 1.13 names the chunk's methods a and d, 1.13.2's setType and d, and from 1.14 on setType and removeTileEntity; its setter is setTypeAndData up
    // to 1.15.2 and a, with a second number, from 1.16.
    private static ClassPatch PaperPatch(string package)
    {
        string chunk = package + "IChunkAccess";
        string region = package + "RegionLimitedWorldAccess";
        string position = "L" + package + "BlockPosition;";
        string state = "L" + package + "IBlockData;";
        string remove = "(" + position + ")V";
        string set = "(" + position + state + "Z)" + state;
        JavaMethod[] setters = [JavaMethod.Virtual(region, name: "setTypeAndData", "(" + position + state + "I)Z"), JavaMethod.Virtual(region, name: "a", "(" + position + state + "II)Z")];
        JavaMethod[] blocks = [JavaMethod.Virtual(package + "IBlockData", name: "getBlock", "()L" + package + "Block;")];

        ChunkCalls[] chunks =
        [
            new(JavaMethod.Interface(chunk, name: "a", set), JavaMethod.Interface(chunk, name: "d", remove)),
            new(JavaMethod.Interface(chunk, name: "setType", set), JavaMethod.Interface(chunk, name: "d", remove)),
            new(JavaMethod.Interface(chunk, name: "setType", set), JavaMethod.Interface(chunk, name: "removeTileEntity", remove)),
        ];

        return ClassPatch.For(region).Edit(Description, context => RemoveStale(context.Editor, setters, chunks, blocks));
    }

    // The setter is the first of the given ones that the class has. It must call a chunk's setBlockState and removeTileEntity of one naming, and getBlock on the
    // state it takes, whose class its descriptor names second; a class that doesn't, as another release's class of the same name, or one patched already, is
    // left alone. The added method sets the block as the region did, has the helper remove the chunk's block entity when the blocks differ, and returns the
    // replaced state: modloaderfixes$setBlockState(chunk, position, state, moving). Its two lambdas, the state's getBlock and the chunk's removeTileEntity, take
    // their receiver as an argument and capture nothing, so the JVM creates each once.
    private static int RemoveStale(ClassFileEditor editor, IReadOnlyList<JavaMethod> setters, IReadOnlyList<ChunkCalls> chunks, IReadOnlyList<JavaMethod> blocks)
    {
        JavaClassFile model = editor.Model;

        foreach (JavaMethod setter in setters)
        {
            if (model.FindMethod(setter.Name, setter.Descriptor)?.Code?.Instructions is not { } code)
                continue;

            string state = SecondParameter(setter.Descriptor);
            JavaMethod[] gets = [.. blocks.SelectMany(block => StateCalls(state, block)).Where(get => Calls(model, code, get))];
            ChunkCalls? chunk = chunks.FirstOrDefault(chunk => Calls(model, code, chunk.Set) && Calls(model, code, chunk.Remove));

            if (gets is not [var get, ..] || chunk is null)
                return 0;

            string added = "(L" + chunk.Set.Owner + ";" + chunk.Set.Descriptor[1..];

            if (model.FindMethod(Added, added) is not null)
                return 0;

            editor.AddMethod(
                PrivateStaticSynthetic,
                Added,
                added,
                builder => builder.ALoad(slot: 0)
                    .ALoad(slot: 1)
                    .ALoad(slot: 2)
                    .ILoad(slot: 3)
                    .Invoke(chunk.Set)
                    .AStore(slot: 4)
                    .ALoad(slot: 4)
                    .ALoad(slot: 2)
                    .Lambda(Apply, get)
                    .ALoad(slot: 0)
                    .ALoad(slot: 1)
                    .Lambda(Accept, chunk.Remove)
                    .Invoke(RemoveEntityOfOtherBlock)
                    .ALoad(slot: 4)
                    .AReturn()
            );

            return editor.ReplaceInvocation(chunk.Set, model.Name, Added, added, setter.Name, setter.Descriptor, ownerIsInterface: false);
        }

        return 0;
    }

    private static string SecondParameter(string descriptor)
    {
        int start = descriptor.IndexOf(value: ';', StringComparison.Ordinal) + 2;

        return descriptor[start..descriptor.IndexOf(value: ';', start)];
    }

    private static string SetDescriptor(string state)
    {
        return "(" + Position + state + "Z)" + state;
    }

    // The state is an interface in 1.13 and a class from 1.14, and the region calls getBlock on it accordingly.
    private static JavaMethod[] StateCalls(string state, JavaMethod block)
    {
        return [JavaMethod.Virtual(state, block.Name, block.Descriptor), JavaMethod.Interface(state, block.Name, block.Descriptor)];
    }

    private static ClassPatch VanillaPatch(ClassNames names, JavaMethod setter)
    {
        return names.Patch().Edit(Description, context => RemoveStale(context.Editor, [setter], Chunks, Blocks));
    }

    // A chunk's setBlockState and removeTileEntity under one naming.
    private sealed record ChunkCalls(JavaMethod Set, JavaMethod Remove);

    // The region's MappedClass item of a range of releases, with its setter's name and descriptor under Forge.
    private sealed record Region(MappedClass Mapped, string Setter, string Descriptor);
}
