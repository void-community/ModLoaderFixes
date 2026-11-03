using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;

namespace ModLoaderFixes;

/// <summary>
/// Makes a Minecraft client handle the packets a server sends just before it disconnects the client, such as the chat that explains a kick. The server sends the
/// chat, then the Disconnect, and closes its end. The client's network thread hands most packets to the client thread, but handles the Disconnect itself and closes
/// the connection at once. Minecraft 1.7 queues the chat, and its client tick handles the queue only while the connection is open, so it never handles a chat
/// that came before a Disconnect. Minecraft 1.8 to 1.12.2 schedule the chat on the client thread, which runs it whatever the connection, so they show it.
/// Minecraft 1.13 and later schedule it too, but once the client thread finds the connection closed it leaves the world and drops the tasks still queued, and from
/// 1.14.4 it also drops a packet it finds the connection closed for, as it often is by the time it gets to the chat.
/// </summary>
internal static class FinalPackets
{
    private const string Description = "packets before a disconnect handled first";
    private const string Helper = "modloaderfixes/QueuedPackets";
    private const string MovedDisconnect = "modloaderfixes$handleDisconnect";
    private const int PrivateStaticSynthetic = 0x100A;
    private const string ProcessThenCheck = "modloaderfixes$processReceivedPackets";
    private const string Schedule = "(Ljava/lang/Runnable;)Lcom/google/common/util/concurrent/ListenableFuture;";
    private const int Static = 0x0008;

    private static readonly JavaMethod AwaitHandled = JavaMethod.Static(Helper, name: "awaitHandled", descriptor: "(Ljava/util/concurrent/Executor;)V");
    private static readonly JavaMethod AwaitScheduled = JavaMethod.Static(Helper, name: "awaitHandled", descriptor: "(Ljava/util/concurrent/Future;)V");
    private static readonly JavaMethod Nothing = JavaMethod.Static(Helper, name: "nothing", descriptor: "()Ljava/lang/Runnable;");

    /// <summary>
    /// Gets the patches: of Minecraft 1.7's client tick in <c>PlayerControllerMP</c>, and of the Disconnect's handler in the client's packet listener from 1.13,
    /// <c>NetHandlerPlayClient</c> up to 1.13.2, <c>ClientPlayNetHandler</c> up to 1.16.5, <c>ClientPacketListener</c> up to 1.20.1 and
    /// <c>ClientCommonPacketListenerImpl</c> from 1.20.2, each by Forge's names and by those of each vanilla release.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. MappedClasses.PlayerController.Patches(Process),
        .. MappedClasses.PlayClientHandler.Patches(
            static names => Await(names, name: "func_147253_a", descriptor: "(Lnet/minecraft/network/play/server/SPacketDisconnect;)V", isScheduled: true)
        ),
        .. MappedClasses.PlayNetHandler.Patches(static names => Await(names, name: "func_147253_a", descriptor: "(Lnet/minecraft/network/play/server/SDisconnectPacket;)V")),
        .. MappedClasses.PacketListener.Patches(
            static names => Await(names, name: "handleDisconnect", descriptor: "(Lnet/minecraft/network/protocol/game/ClientboundDisconnectPacket;)V")
        ),
        .. MappedClasses.CommonPacketListener.Patches(
            static names => Await(names, name: "handleDisconnect", descriptor: "(Lnet/minecraft/network/protocol/common/ClientboundDisconnectPacket;)V")
        ),
    ];

    private static ClassPatch Await(ClassNames names, string name, string descriptor, bool isScheduled = false)
    {
        string handler = names.Virtual(name, descriptor).Descriptor;

        return names.Patch().Edit(Description, context => Await(context.Editor, handler, isScheduled));
    }

    // The Disconnect's handler is the one method that takes the packet, which Forge from 1.17 to 1.20.4 names by its SRG name, so it is found by its descriptor.
    // The client is the listener's one field of the type that its constructors take first. The handler waits for the client thread, then runs as before, on the
    // network thread, which reads nothing more from the connection until it returns. From 1.14 the client is an executor; a 1.13 client queues a task with a
    // method of its own instead, which the listener calls itself when a server asks it to download a resource pack, so the method is found among those calls.
    private static int Await(ClassFileEditor editor, string descriptor, bool isScheduled)
    {
        JavaClassFile model = editor.Model;
        ClassMember[] handlers = [.. model.Methods.Where(method => method.Descriptor == descriptor && (method.AccessFlags & Static) == 0)];
        string[] clients = [.. model.Methods.Where(static method => method.Name == "<init>").Select(static method => FirstParameter(method.Descriptor)).Distinct(StringComparer.Ordinal)];
        ClassMember[] fields = clients is [{ } client] ? [.. model.Fields.Where(field => field.Descriptor == client && (field.AccessFlags & Static) == 0)] : [];
        bool isPatched = model.FindMethod(MovedDisconnect, descriptor) is not null;

        if (handlers is not [{ } handler] || fields is not [{ } minecraft] || isPatched)
            return 0;

        JavaMethod[] schedulers = isScheduled ? Schedulers(model, minecraft.Descriptor[1..^1]) : [];

        if (isScheduled && schedulers is not [_])
            return 0;

        // Waits with the client on the stack: queues a task through the 1.13 client's method and waits on its future, or hands a later client to the helper.
        Func<CodeBuilder, CodeBuilder> wait = schedulers is [{ } scheduler]
            ? code => code.Invoke(Nothing).Invoke(scheduler).Invoke(AwaitScheduled)
            : static code => code.Invoke(AwaitHandled);

        editor.ReplaceMethodBody(
            handler.Name,
            descriptor,
            MovedDisconnect,
            code => wait(code.ALoad(slot: 0).GetField(model.Name, minecraft.Name, minecraft.Descriptor))
                .ALoad(slot: 0)
                .ALoad(slot: 1)
                .InvokeSpecial(model.Name, MovedDisconnect, descriptor)
                .Return()
        );

        return 1;
    }

    private static MemberReference? Called(JavaClassFile model, Instruction instruction)
    {
        return instruction is { Opcode: Opcodes.InvokeVirtual, ConstantIndex: { } index } ? model.GetMemberReference(index) : null;
    }

    private static string FirstParameter(string descriptor)
    {
        int end = descriptor.IndexOf(value: ';', StringComparison.Ordinal);

        return descriptor.StartsWith(value: "(L", StringComparison.Ordinal) && end > 0 ? descriptor[1..(end + 1)] : string.Empty;
    }

    private static ClassPatch Process(ClassNames names)
    {
        JavaMethod tick = names.Virtual(name: "func_78765_e", descriptor: "()V");

        return names.Patch().Edit(Description, context => Process(context.Editor, tick));
    }

    // updateController checks whether the connection is open, processes the received packets when it is, and otherwise reports the disconnect to the listener:
    // ..., invokevirtual NetworkManager.isChannelOpen()Z, ifeq disconnected, aload_0, getfield, invokevirtual getNetworkManager, invokevirtual
    // NetworkManager.processReceivedPackets()V, goto end, disconnected: .... The check becomes a method that processes the packets first and then checks, and the
    // processing in the open branch a pop of the manager, at the same lengths, so the tick processes the packets once either way and only then reports a disconnect.
    private static int Process(ClassFileEditor editor, JavaMethod tick)
    {
        JavaClassFile model = editor.Model;
        IReadOnlyList<Instruction>? code = model.FindMethod(tick.Name, tick.Descriptor)?.Code?.Instructions;
        int check = (code?.ToList().FindIndex(static instruction => instruction.Opcode == Opcodes.IfEq) - 1) ?? -1;

        if (code is null || check < 0 || code.Count < check + 7 || model.FindMethod(ProcessThenCheck) is not null)
            return 0;

        if (Called(model, code[check]) is not { Descriptor: "()Z" } isOpen)
            return 0;

        if (Called(model, code[check + 5]) is not { Descriptor: "()V" } processes)
            return 0;

        if (processes.Owner != isOpen.Owner || code[check + 6].Opcode != Opcodes.Goto)
            return 0;

        JavaMethod isChannelOpen = JavaMethod.Virtual(isOpen.Owner, isOpen.Name, isOpen.Descriptor);
        JavaMethod processReceivedPackets = JavaMethod.Virtual(processes.Owner, processes.Name, processes.Descriptor);
        JavaMethod processThenCheck = JavaMethod.Static(model.Name, ProcessThenCheck, "(L" + isOpen.Owner + ";)Z");

        editor.AddMethod(
            PrivateStaticSynthetic,
            ProcessThenCheck,
            processThenCheck.Descriptor,
            builder => builder.ALoad(slot: 0).Invoke(processReceivedPackets).ALoad(slot: 0).Invoke(isChannelOpen).IReturn()
        );

        bool isCheckReplaced = editor.ReplaceCode(
            tick.Name,
            tick.Descriptor,
            code[check].Offset,
            editor.AssembleCode(builder => builder.Invoke(isChannelOpen)),
            editor.AssembleCode(builder => builder.Invoke(processThenCheck))
        );

        bool isProcessReplaced = isCheckReplaced && editor.ReplaceCode(
            tick.Name,
            tick.Descriptor,
            code[check + 5].Offset,
            editor.AssembleCode(builder => builder.Invoke(processReceivedPackets)),
            editor.AssembleCode(static builder => builder.Pop().Nop().Nop())
        );

        return isProcessReplaced ? 1 : throw new InvalidOperationException(model.Name + "." + tick.Name + " doesn't call the connection's methods it was read to call.");
    }

    private static JavaMethod[] Schedulers(JavaClassFile model, string client)
    {
        IEnumerable<MemberReference?> called = model.Methods.SelectMany(static method => method.Code?.Instructions ?? []).Select(instruction => Called(model, instruction));

        return
        [
            .. called.Where(member => member is { Descriptor: Schedule } reference && reference.Owner == client)
                .Select(static member => member.GetValueOrDefault().Name)
                .Distinct(StringComparer.Ordinal)
                .Select(name => JavaMethod.Virtual(client, name, Schedule)),
        ];
    }
}
