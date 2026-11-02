using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Lets one thread at a time flush the queue of packets that a Minecraft 1.7.2 to 1.14.3 connection keeps while it has no channel. A client sends its handshake
/// from the thread that connects before the channel is active, so the handshake waits in the queue, and then its login start, which flushes the queue first.
/// Meanwhile the client thread's tick of the connecting screen flushes the same queue. The flush loops on <c>isEmpty</c> and <c>poll</c> with nothing to keep two
/// flushes apart, since 1.8.2 to 1.14.3 hold only the shared read lock of the manager's <c>ReentrantReadWriteLock</c>. In 1.7 the tick could take the handshake
/// off the queue while the login start went out and switched the channel to the login state, and the handshake then reached the encoder in the login state,
/// which has no id for it: <c>Can't serialize unregistered packet</c>. From 1.8 both flushes can find the one packet there, and the second polls null and crashes
/// the client with <c>Ticking screen</c>. With the flush synchronized on the manager, a send, which flushes before it dispatches its own packet, waits for a flush
/// that is still under way. The flush waits for nothing while it holds the manager, since it only queues its writes on the event loop. Minecraft 1.14.4 polls
/// until the queue is empty under the queue's monitor, so a flush that never calls <c>isEmpty</c> is left alone.
/// </summary>
internal static class OutboundPackets
{
    private const string Description = "outbound queue flushed by one thread at a time";
    private const int Synchronized = 0x0020;

    private static readonly JavaMethod IsEmpty = JavaMethod.Interface(owner: "java/util/Queue", name: "isEmpty", descriptor: "()Z");

    /// <summary>Gets the patches of <c>NetworkManager</c>'s flush, <c>flushOutboundQueue</c>: Forge's, by FML's names, and vanilla's under each release's names.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = MappedClasses.NetworkManager.Patches(Synchronize);

    // A manager that lacks the flush under the naming's names, as another release's class of the same name does, is left alone with the message.
    private static ClassPatch Synchronize(ClassNames names)
    {
        JavaMethod flush = names.Virtual(name: "func_150733_h", descriptor: "()V");

        return names.Patch().Edit(Description, context => Synchronize(context.Editor, flush));
    }

    private static int Synchronize(ClassFileEditor editor, JavaMethod flush)
    {
        JavaClassFile model = editor.Model;
        ClassMember member = model.FindMethod(flush.Name, flush.Descriptor) ?? throw new InvalidOperationException(model.Name + " has no " + flush.Name + flush.Descriptor + ".");
        bool checksEmptiness = member.Code?.Instructions.Any(instruction => model.Invokes(instruction, IsEmpty)) ?? false;
        bool isSynchronized = (member.AccessFlags & Synchronized) != 0;

        return !checksEmptiness || isSynchronized ? 0 : editor.SetMethodAccess(member.Name, member.Descriptor, member.AccessFlags | Synchronized);
    }
}
