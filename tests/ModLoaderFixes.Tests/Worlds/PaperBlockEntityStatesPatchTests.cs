using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Worlds;

using Xunit;

namespace ModLoaderFixes.Tests.Worlds;

/// <summary>
/// Pointing the cast of a block's block entity in the constructor of Paper 1.14's and Paper 1.17's CraftBlockEntityState at the helper that gives a block with
/// another block's block entity its own.
/// </summary>
public sealed class PaperBlockEntityStatesPatchTests
{
    private const string Cast = "(Ljava/lang/Object;)Ljava/lang/Object;";
    private const string Constructor = "(Lorg/bukkit/block/Block;Ljava/lang/Class;)V";
    private const string HelperDescriptor = "(Ljava/lang/Class;Ljava/lang/Object;)Ljava/lang/Object;";

    /// <summary>
    /// Paper 1.17.1 keeps the name of 1.17's state and builds it from a block entity it was given, without the constructor that casts, so its class is left alone,
    /// and without a reason, which would otherwise be logged at every start of Paper 1.17.1.
    /// </summary>
    [Fact]
    public void PatchLeavesAStateWithoutTheCastingConstructor()
    {
        byte[] classFile = new ClassFileWriter(name: "org/bukkit/craftbukkit/v1_17_R1/block/CraftBlockEntityState", majorVersion: 60)
            .AddMethod(
                name: "<init>",
                descriptor: "(Lorg/bukkit/World;Lnet/minecraft/world/level/block/entity/TileEntity;)V",
                static code => code.Return(),
                accessFlags: 0x0001
            )
            .ToArray();

        Assert.All(
            PaperBlockEntityStates.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)),
            patch => Assert.False(patch.Apply(classFile).Changed)
        );
    }

    /// <summary>The cast in another method of the state, as in the constructor that takes a block entity, stays Java's.</summary>
    [Fact]
    public void PatchLeavesTheCastOfOtherMethods()
    {
        const string state = "org/bukkit/craftbukkit/v1_14_R1/block/CraftBlockEntityState";

        byte[] patched = PaperBlockEntityStates.Paper14.Apply(Create(state, entity: "net/minecraft/server/v1_14_R1/TileEntity")).Bytes ?? throw new InvalidOperationException();

        ClassFileAssert.Calls(patched, method: "load", owner: "java/lang/Class", name: "cast", Cast);
    }

    /// <summary>The constructor of each release's state casts through the helper that names that release's classes, and no longer through the class itself.</summary>
    /// <param name="state">The state's name.</param>
    /// <param name="entity">The name of the block entity it holds.</param>
    /// <param name="helper">The helper its constructor casts through.</param>
    [Theory]
    [InlineData(
        "org/bukkit/craftbukkit/v1_14_R1/block/CraftBlockEntityState",
        "net/minecraft/server/v1_14_R1/TileEntity",
        "modloaderfixes/PaperBlockEntities"
    )]
    [InlineData(
        "org/bukkit/craftbukkit/v1_17_R1/block/CraftBlockEntityState",
        "net/minecraft/world/level/block/entity/TileEntity",
        "modloaderfixes/Paper17BlockEntities"
    )]
    public void PatchRedirectsTheCastOfTheConstructor(string state, string entity, string helper)
    {
        byte[] classFile = Create(state, entity);

        ClassPatchResult result = Assert.Single(PaperBlockEntityStates.Patches, patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, method: "<init>", helper, name: "cast", HelperDescriptor);
        ClassFileAssert.DoesNotCall(patched, method: "<init>", owner: "java/lang/Class", name: "cast", Cast);
    }

    // The state as far as the patch looks at it: a constructor that casts what it is given to the class it is given and keeps it, as CraftBukkit's casts the
    // block entity that the world has at the block's position, and another method that casts too.
    private static byte[] Create(string state, string entity)
    {
        string entityDescriptor = "L" + entity + ";";
        JavaMethod cast = JavaMethod.Virtual(owner: "java/lang/Class", name: "cast", Cast);

        return new ClassFileWriter(state, majorVersion: 52)
            .AddField(name: "tileEntity", entityDescriptor)
            .AddMethod(
                name: "<init>",
                Constructor,
                code => code.ALoad(slot: 0)
                    .InvokeSpecial(owner: "java/lang/Object", name: "<init>", descriptor: "()V")
                    .ALoad(slot: 0)
                    .ALoad(slot: 2)
                    .ALoad(slot: 1)
                    .Invoke(cast)
                    .CheckCast(entity)
                    .PutField(state, name: "tileEntity", entityDescriptor)
                    .Return(),
                accessFlags: 0x0001
            )
            .AddMethod(
                name: "load",
                descriptor: "(Ljava/lang/Class;Ljava/lang/Object;)V",
                code => code.ALoad(slot: 1).ALoad(slot: 2).Invoke(cast).Pop().Return(),
                accessFlags: 0x0001
            )
            .ToArray();
    }
}
