using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes the two methods of Minecraft 1.7's <c>NetworkManager</c> that take packets off its outbound queue synchronized: the one that sends a packet, which
/// queues it while the connection has no channel and otherwise flushes the queue and dispatches it, and the flush, which the network tick runs too. Vanilla's
/// names change in every release, so it also finds them by their shape: the send and the dispatch both take a packet and Netty's listeners, the dispatch is
/// private, and the flush takes nothing and calls the dispatch.
/// </summary>
internal static class PacketQueue
{
    private const string ListenersSuffix = ";[Lio/netty/util/concurrent/GenericFutureListener;)V";
    private const int Private = 0x0002;
    private const int Static = 0x0008;
    private const int Synchronized = 0x0020;

    /// <summary>Synchronizes the send and the flush that a class declares under known names.</summary>
    /// <param name="editor">The class.</param>
    /// <param name="send">The send's name and descriptor.</param>
    /// <param name="flush">The flush's name and descriptor.</param>
    /// <returns>The methods synchronized, two, or fewer when some already were.</returns>
    internal static int Synchronize(ClassFileEditor editor, (string Name, string Descriptor) send, (string Name, string Descriptor) flush)
    {
        ArgumentNullException.ThrowIfNull(editor);

        return Synchronize(editor, Find(editor.Model, send)) + Synchronize(editor, Find(editor.Model, flush));
    }

    /// <summary>
    /// Synchronizes the send and the flush of a class that has the shape of vanilla 1.7's <c>NetworkManager</c>, whatever their names. A class without exactly one
    /// of each, as Minecraft 1.8's, which sends with an extra listener, isn't that manager and is left alone.
    /// </summary>
    /// <param name="context">The class.</param>
    /// <returns>The methods synchronized, two, or none for a class that isn't the manager.</returns>
    internal static int SynchronizeByShape(ClassEditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        JavaClassFile model = context.Editor.Model;
        ClassMember[] dispatches = [.. model.Methods.Where(method => TakesPacket(method) && (method.AccessFlags & Private) != 0)];
        ClassMember[] sends = [.. model.Methods.Where(method => TakesPacket(method) && (method.AccessFlags & Private) == 0)];

        if (dispatches.Length != 1 || sends.Length != 1)
            return 0;

        ClassMember[] flushes = [.. model.Methods.Where(method => method.Descriptor == "()V" && Calls(model, method, dispatches[0]))];

        return flushes.Length != 1 ? 0 : Synchronize(context.Editor, sends[0]) + Synchronize(context.Editor, flushes[0]);
    }

    private static bool Calls(JavaClassFile model, ClassMember method, ClassMember target)
    {
        return method.Code?.Instructions.Any(
                instruction =>
                instruction.Opcode is Opcodes.InvokeSpecial or Opcodes.InvokeVirtual
                && instruction.ConstantIndex is { } index
                && model.GetMemberReference(index) is { } reference
                && reference.Name == target.Name
                && reference.Descriptor == target.Descriptor
            )
            ?? false;
    }

    private static ClassMember Find(JavaClassFile model, (string Name, string Descriptor) method)
    {
        return model.FindMethod(method.Name, method.Descriptor) ?? throw new InvalidOperationException($"{model.Name} has no {method.Name}{method.Descriptor}.");
    }

    private static int Synchronize(ClassFileEditor editor, ClassMember method)
    {
        return (method.AccessFlags & Synchronized) != 0 ? 0 : editor.SetMethodAccess(method.Name, method.Descriptor, method.AccessFlags | Synchronized);
    }

    // An instance method that takes one packet and Netty's listeners, as the send and the dispatch do.
    private static bool TakesPacket(ClassMember method)
    {
        string descriptor = method.Descriptor;
        bool isInstance = (method.AccessFlags & Static) == 0;

        return isInstance && descriptor.StartsWith(value: "(L", StringComparison.Ordinal) && descriptor.EndsWith(ListenersSuffix, StringComparison.Ordinal)
            && descriptor.IndexOf(value: ';', StringComparison.Ordinal) == descriptor.Length - ListenersSuffix.Length;
    }
}
