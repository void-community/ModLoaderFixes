using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Locks the set of the listeners that a Minecraft reload still waits for. <c>SimpleReloadInstance</c> keeps them in a plain <c>HashSet</c>, and each listener,
/// as its preparation reaches the reload's barrier, hands the reload's game executor a task that removes it from the set and completes the barrier once the set
/// is empty. A dedicated server's startup in Minecraft 1.16 to 1.18.2 passes <c>Runnable::run</c> as that executor, so the tasks run on the worker threads, two of
/// them can remove at once and lose a removal, and the server waits for its data packs forever. The patches point the constructor's <c>Sets.newHashSet</c> at
/// <c>modloaderfixes.ReloadListeners</c>, whose set locks each method. Vanilla, Paper and Spigot servers and Forge's from 1.16 to 1.18.2 have the race. Forge
/// keeps the same code under MCP's name from 1.14, and Forge, NeoForge and Paper under Mojang's or CraftBukkit's up to 1.21.4, where only the game's thread uses
/// the set and the lock changes nothing; 1.21.5 creates the set otherwise.
/// </summary>
internal static class ReloadListeners
{
    private static readonly JavaMethod NewHashSet = JavaMethod.Static(owner: "com/google/common/collect/Sets", name: "newHashSet", descriptor: "(Ljava/lang/Iterable;)Ljava/util/HashSet;");

    /// <summary>
    /// Gets the patches of the reload: Forge's, by MCP's name <c>AsyncReloader</c> up to 1.16.5 and Mojang's <c>SimpleReloadInstance</c> from 1.17, vanilla's under
    /// each release's names up to 1.18.2, and CraftBukkit's <c>Reloadable</c>, which Paper and Spigot keep in a package of each 1.16 revision and then in Mojang's.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. MappedClasses.AsyncReloader.Patches(Lock),
        .. MappedClasses.SimpleReloadInstance.Patches(Lock),
        .. new[] { "v1_16_R1/Reloadable", "v1_16_R2/Reloadable", "v1_16_R3/Reloadable", "packs/resources/Reloadable" }.Select(static name => Lock(ClassPatch.For("net/minecraft/server/" + name), "net/minecraft/server/" + name)),
    ];

    private static ClassPatch Lock(ClassNames names)
    {
        return Lock(names.Patch(), names.Name);
    }

    // The constructor creates the set and then, for each listener, the barrier, the reload's first anonymous class, which another release's class of the same
    // obfuscated name doesn't.
    private static ClassPatch Lock(ClassPatch patch, string className)
    {
        string barrier = className + "$1";

        return patch.Method(name: "<init>", descriptor: null)
            .When(body => body.Calls(NewHashSet) && body.Creates(barrier))
            .RedirectInvocation(NewHashSet, toOwner: "modloaderfixes/ReloadListeners");
    }
}
