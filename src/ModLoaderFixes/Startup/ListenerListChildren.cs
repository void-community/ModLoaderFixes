using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Makes the children of an EventBus listener list safe to add to while they are iterated, as EventBus 4.0.0 does. <c>ListenerList.ListenerListInst</c> keeps
/// the lists of its event's subclasses in a plain <c>ArrayList</c>: <c>addChild</c> creates and fills it as a subclass's list is first built, under the new list's
/// monitor only, and <c>forceRebuild</c> iterates it after a listener registers, without a lock. Mods construct at once on the mod-loading workers, so one that
/// adds a listener can iterate a parent's children while another's first use of a subclass adds to them, and FML fails the first mod with
/// <c>ConcurrentModificationException</c>; two first adds can also both create the list and lose a child. Every EventBus before 4.0.0 has the code, which Forge
/// ships from 1.13.2 to 1.16.4-35.1.36. The patch makes <c>addChild</c> synchronized and its list one from <c>modloaderfixes.ListenerChildren</c> that locks itself,
/// and points <c>forceRebuild</c>'s iterator at the helper, which copies the list under its lock. EventBus 4.0.0 and later create a synchronized list already and
/// are left alone.
/// </summary>
internal static class ListenerListChildren
{
    private const string AddChild = "addChild";
    private const string Helper = "modloaderfixes/ListenerChildren";
    // ACC_PRIVATE | ACC_SYNCHRONIZED: addChild stays private and locks the parent list it adds to.
    private const int PrivateSynchronized = 0x0022;

    private static readonly JavaMethod Iterator = JavaMethod.Interface(owner: "java/util/List", name: "iterator", descriptor: "()Ljava/util/Iterator;");
    private static readonly JavaMethod SynchronizedList = JavaMethod.Static(owner: "java/util/Collections", name: "synchronizedList", descriptor: "(Ljava/util/List;)Ljava/util/List;");

    /// <summary>Gets the patch of <c>ListenerList$ListenerListInst</c>, which every EventBus up to 6.2 names so.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraftforge/eventbus/ListenerList$ListenerListInst")
        .Method(AddChild, descriptor: "(Lnet/minecraftforge/eventbus/ListenerList$ListenerListInst;)V")
        .When(static body => !body.Calls(SynchronizedList))
        .ReplaceConstruction(
            fromClass: "java/util/ArrayList",
            JavaMethod.Static(Helper, name: "newChildren", descriptor: "()Ljava/util/List;"),
            storedIn: "children"
        )
        .SetAccess(PrivateSynchronized)
        .Method(name: "forceRebuild", descriptor: "()V")
        .RedirectInvocation(Iterator, Helper);
}
