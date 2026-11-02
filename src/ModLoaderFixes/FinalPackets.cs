using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes a Minecraft client handle the packets a server sends just before it disconnects the client, such as the chat that explains a kick. The server sends the
/// chat, then the Disconnect, and closes its end. The client's network thread hands most packets to the client thread, but handles the Disconnect itself and closes
/// the connection at once. Minecraft 1.8 to 1.14.3 schedule the chat on the client thread, which runs it whatever the connection, so they show it. Minecraft 1.14.4
/// and later schedule it too, but the client thread drops a packet once the connection is closed, as it often is by the time it gets to the chat.
/// </summary>
internal static class FinalPackets
{
    private const string Description = "packets before a disconnect handled first";
    private const string Helper = "modloaderfixes/QueuedPackets";
    private const string MovedDisconnect = "modloaderfixes$handleDisconnect";
    private const int Static = 0x0008;

    private static readonly JavaMethod AwaitHandled = JavaMethod.Static(Helper, name: "awaitHandled", descriptor: "(Ljava/util/concurrent/Executor;)V");

    /// <summary>
    /// Gets the patches of the Disconnect's handler in the client's packet listener from 1.14.4: <c>ClientPlayNetHandler</c> up to 1.16.5, <c>ClientPacketListener</c>
    /// up to 1.20.1 and <c>ClientCommonPacketListenerImpl</c> from 1.20.2, each by Forge's names and by those of each vanilla release.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. MappedClasses.PlayNetHandler.Patches(static names => Await(names, name: "func_147253_a", descriptor: "(Lnet/minecraft/network/play/server/SDisconnectPacket;)V")),
        .. MappedClasses.PacketListener.Patches(
            static names => Await(names, name: "handleDisconnect", descriptor: "(Lnet/minecraft/network/protocol/game/ClientboundDisconnectPacket;)V")
        ),
        .. MappedClasses.CommonPacketListener.Patches(
            static names => Await(names, name: "handleDisconnect", descriptor: "(Lnet/minecraft/network/protocol/common/ClientboundDisconnectPacket;)V")
        ),
    ];

    private static ClassPatch Await(ClassNames names, string name, string descriptor)
    {
        string handler = names.Virtual(name, descriptor).Descriptor;

        return names.Patch().Edit(Description, context => Await(context.Editor, handler));
    }

    // The Disconnect's handler is the one method that takes the packet, which Forge from 1.17 to 1.20.4 names by its SRG name, so it is found by its descriptor.
    // The client is the listener's one field of the type that its constructors take first. The handler waits for the client thread, then runs as before, on the
    // network thread, which reads nothing more from the connection until it returns.
    private static int Await(ClassFileEditor editor, string descriptor)
    {
        JavaClassFile model = editor.Model;
        ClassMember[] handlers = [.. model.Methods.Where(method => method.Descriptor == descriptor && (method.AccessFlags & Static) == 0)];
        string[] clients = [.. model.Methods.Where(static method => method.Name == "<init>").Select(static method => FirstParameter(method.Descriptor)).Distinct(StringComparer.Ordinal)];
        ClassMember[] fields = clients is [{ } client] ? [.. model.Fields.Where(field => field.Descriptor == client && (field.AccessFlags & Static) == 0)] : [];
        bool isPatched = model.FindMethod(MovedDisconnect, descriptor) is not null;

        if (handlers is not [{ } handler] || fields is not [{ } minecraft] || isPatched)
            return 0;

        editor.ReplaceMethodBody(
            handler.Name,
            descriptor,
            MovedDisconnect,
            code => code.ALoad(slot: 0)
                .GetField(model.Name, minecraft.Name, minecraft.Descriptor)
                .Invoke(AwaitHandled)
                .ALoad(slot: 0)
                .ALoad(slot: 1)
                .InvokeSpecial(model.Name, MovedDisconnect, descriptor)
                .Return()
        );

        return 1;
    }

    private static string FirstParameter(string descriptor)
    {
        int end = descriptor.IndexOf(value: ';', StringComparison.Ordinal);

        return descriptor.StartsWith(value: "(L", StringComparison.Ordinal) && end > 0 ? descriptor[1..(end + 1)] : string.Empty;
    }
}
