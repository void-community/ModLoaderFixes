using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Worlds;

/// <summary>
/// Lets Bukkit read the state of a block that has another block's block entity on Paper 1.14 and 1.17. <c>CraftBlockEntityState</c>'s constructor casts the block
/// entity at the block's position to the class that the block's state expects. A spawner with a chest's block entity, which world generation up to Minecraft
/// 1.16.5 leaves where a dungeon's centre lands on an earlier dungeon's chest, fails that cast, and Paper's timings history, which reads the state of every block
/// entity of the loaded chunks every five minutes, stops the server with a <c>ClassCastException</c>. CraftBukkit replaces such a block entity as the world
/// hands it out, but that code is missing from Paper 1.14 to 1.14.4 before build 217, and Paper 1.17's asks the block's state, where it means the block, whether
/// it can create a block entity, so it replaces nothing. The patches point the constructor's <c>Class.cast</c> at a helper, which gives a block whose block
/// entity fails the cast the one that the block creates, as CraftBukkit's repair does, and casts as before otherwise. Paper 1.17 keeps CraftBukkit's names in
/// Mojang's packages, so it has a helper of its own; Paper 1.17.1, of the same CraftBukkit revision, has no such constructor.
/// </summary>
internal static class PaperBlockEntityStates
{
    private const string Constructor = "(Lorg/bukkit/block/Block;Ljava/lang/Class;)V";
    private const string State17 = "org/bukkit/craftbukkit/v1_17_R1/block/CraftBlockEntityState";

    private static readonly JavaMethod Cast = JavaMethod.Virtual(owner: "java/lang/Class", name: "cast", descriptor: "(Ljava/lang/Object;)Ljava/lang/Object;");

    /// <summary>Gets the patch of Paper 1.14's state, whose block entities <c>modloaderfixes.PaperBlockEntities</c> names by the package of that revision.</summary>
    internal static ClassPatch Paper14 { get; } = ClassPatch.For(className: "org/bukkit/craftbukkit/v1_14_R1/block/CraftBlockEntityState")
        .Method(name: "<init>", Constructor)
        .RedirectInvocation(Cast, toOwner: "modloaderfixes/PaperBlockEntities");

    /// <summary>
    /// Gets the patch of Paper 1.17's state, whose block entities <c>modloaderfixes.Paper17BlockEntities</c> names by Mojang's packages. It filters by name, so
    /// that Paper 1.17.1's class of the same name, which lacks the constructor, is skipped quietly.
    /// </summary>
    internal static ClassPatch Paper17 { get; } = ClassPatch.ForMatching(static name => name == State17, State17 + " of Paper 1.17")
        .Method(name: "<init>", Constructor)
        .RedirectInvocation(Cast, toOwner: "modloaderfixes/Paper17BlockEntities");

    /// <summary>Gets the patches of the state's constructor in Paper 1.14 and in Paper 1.17.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [Paper14, Paper17];
}
