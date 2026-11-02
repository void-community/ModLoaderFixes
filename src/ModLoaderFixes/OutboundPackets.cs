using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Keeps Minecraft 1.7's connections sending their packets in the order they were sent, and each in the state it was sent in, as a client's first packets need. A
/// client sends its handshake and its login start from the thread that connects, while the client thread's tick of the connecting screen flushes the same queue of
/// packets that waited for the channel. Nothing keeps the two apart: the tick can take the handshake off the queue, the login start can go out meanwhile and switch
/// the channel to the login state, and the handshake then reaches the encoder in the login state, which has no id for it. The client drops the connection with
/// <c>Can't serialize unregistered packet</c>, a few milliseconds after it opened it. With the send and the flush synchronized on the manager, a send waits for a
/// flush that is still under way. Neither waits for anything while it holds the manager, since it only queues its writes on the event loop.
/// </summary>
internal static class OutboundPackets
{
    private const string Description = "packets sent in order";
    private const string SendDescriptor = "(Lnet/minecraft/network/Packet;[Lio/netty/util/concurrent/GenericFutureListener;)V";
    private const int Synchronized = 0x0020;

    /// <summary>
    /// Gets the patches of <c>NetworkManager</c>: Forge's, by FML's names <c>scheduleOutboundPacket</c> and <c>flushOutboundQueue</c>, and vanilla's under each
    /// release's names.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = MappedClasses.NetworkManager.Patches(Synchronize);

    // A manager that lacks either method under the naming's names, as another release's class of the same name does, is left alone with the message.
    private static ClassPatch Synchronize(ClassNames names)
    {
        JavaMethod send = names.Virtual(name: "func_150725_a", SendDescriptor);
        JavaMethod flush = names.Virtual(name: "func_150733_h", descriptor: "()V");

        return names.Patch().Edit(Description, context => Synchronize(context.Editor, send) + Synchronize(context.Editor, flush));
    }

    private static int Synchronize(ClassFileEditor editor, JavaMethod method)
    {
        ClassMember member = editor.Model.FindMethod(method.Name, method.Descriptor)
            ?? throw new InvalidOperationException(editor.Model.Name + " has no " + method.Name + method.Descriptor + ".");

        return (member.AccessFlags & Synchronized) != 0 ? 0 : editor.SetMethodAccess(member.Name, member.Descriptor, member.AccessFlags | Synchronized);
    }
}
