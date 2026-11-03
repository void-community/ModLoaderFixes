using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;

using Xunit;

namespace ModLoaderFixes.Tests.Connections;

/// <summary>
/// Making a Minecraft client's handler of a Disconnect wait until the client thread has handled the packets that came before it, and Minecraft 1.7's client tick
/// handle them before it checks the connection.
/// </summary>
public sealed class FinalPacketsPatchTests
{
    private const string Moved = "modloaderfixes$handleDisconnect";
    private const string ProcessThenCheck = "modloaderfixes$processReceivedPackets";

    /// <summary>
    /// Gets Minecraft 1.7's PlayerControllerMP under Forge's names and vanilla 1.7.10's, with its tick, the play client and its getter of the connection, and the
    /// connection with its check and its processing of the received packets.
    /// </summary>
    public static TheoryData<string, string, string, string, string, string, string> Controllers { get; } = new()
    {
        {
            "net/minecraft/client/multiplayer/PlayerControllerMP", "func_78765_e", "net/minecraft/client/network/NetHandlerPlayClient", "func_147298_b",
            "net/minecraft/network/NetworkManager", "func_150724_d", "func_74428_b"
        },
        { "bje", "e", "bjb", "b", "ej", "d", "a" },
    };

    /// <summary>Gets each packet listener under Forge's names, with its client, its Disconnect packet and the name its handler has, and one under vanilla 1.16.3's.</summary>
    public static TheoryData<string, string, string, string> Listeners { get; } = new()
    {
        { "net/minecraft/client/network/play/ClientPlayNetHandler", "net/minecraft/client/Minecraft", "net/minecraft/network/play/server/SDisconnectPacket", "func_147253_a" },
        { "net/minecraft/client/multiplayer/ClientPacketListener", "net/minecraft/client/Minecraft", "net/minecraft/network/protocol/game/ClientboundDisconnectPacket", "m_7008_" },
        {
            "net/minecraft/client/multiplayer/ClientCommonPacketListenerImpl", "net/minecraft/client/Minecraft",
            "net/minecraft/network/protocol/common/ClientboundDisconnectPacket", "handleDisconnect"
        },
        { "dwm", "djw", "pm", "a" },
    };

    /// <summary>A controller that has the added method already is left as it is.</summary>
    [Fact]
    public void PatchLeavesAPatchedControllerAlone()
    {
        ClassPatchResult first = Apply(CreateController(className: "bje", tick: "e", client: "bjb", getManager: "b", manager: "ej", isOpen: "d", process: "a"));

        Assert.False(Apply(first.Bytes ?? throw new InvalidOperationException(first.ToString())).Changed);
    }

    /// <summary>A listener whose handler has been moved already is left as it is.</summary>
    [Fact]
    public void PatchLeavesAPatchedListenerAlone()
    {
        ClassPatchResult first = Apply(CreateListener(className: "dwm", client: "djw", packet: "pm", handler: "a"));

        Assert.False(Apply(first.Bytes ?? throw new InvalidOperationException(first.ToString())).Changed);
    }

    /// <summary>Another release can give one of its other classes a vanilla release's name; one without the handler and the client is left alone.</summary>
    [Fact]
    public void PatchLeavesOtherClassesOfTheSameNameAlone()
    {
        byte[] classFile = new ClassFileWriter(name: "dwm", superName: "java/lang/Object", accessFlags: 0x0021, majorVersion: 52)
            .AddField(name: "f", descriptor: "Ldjw;", accessFlags: 0x0012)
            .AddMethod(name: "a", descriptor: "(Lpm;)V", static code => code.Return(), accessFlags: 0x0001)
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    /// <summary>Another release can give one of its other classes vanilla 1.7.10's name; one whose method checks a connection but doesn't process it is left alone.</summary>
    [Fact]
    public void PatchLeavesOtherControllersOfTheSameNameAlone()
    {
        byte[] classFile = new ClassFileWriter(name: "bje", superName: "java/lang/Object", accessFlags: 0x0021, majorVersion: 50)
            .AddMethod(
                name: "e",
                descriptor: "()V",
                static code =>
                {
                    CodeLabel done = code.NewLabel();

                    return code.AConstNull().Invoke(JavaMethod.Virtual(owner: "ej", name: "d", descriptor: "()Z")).If(Opcodes.IfEq, done).Mark(done).Return();
                },
                accessFlags: 0x0001
            )
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    /// <summary>
    /// Once patched, the tick's check of the connection calls a method added to the class, which processes the received packets and then checks, and the processing in
    /// the open branch pops the connection instead, so the packets are processed before a closed connection is reported.
    /// </summary>
    /// <param name="className">The controller.</param>
    /// <param name="tick">The name of its tick.</param>
    /// <param name="client">The play client.</param>
    /// <param name="getManager">The name of the client's getter of the connection.</param>
    /// <param name="manager">The connection.</param>
    /// <param name="isOpen">The name of its check.</param>
    /// <param name="process">The name of its processing of the received packets.</param>
    [Theory]
    [MemberData(nameof(Controllers))]
    public void PatchProcessesThePacketsBeforeTheTickChecksTheConnection(string className, string tick, string client, string getManager, string manager, string isOpen, string process)
    {
        ClassPatchResult result = Apply(CreateController(className, tick, client, getManager, manager, isOpen, process));
        JavaClassFile patched = JavaClassFile.Read(result.Bytes ?? throw new InvalidOperationException(result.ToString()));
        CodeAttribute code = patched.FindMethod(tick, descriptor: "()V")?.Code ?? throw new InvalidOperationException(message: "The tick is gone.");
        JavaMethod helper = JavaMethod.Static(className, ProcessThenCheck, "(L" + manager + ";)Z");

        Assert.Equal(
            [
                Opcodes.ALoad0, Opcodes.GetField, Opcodes.InvokeVirtual, Opcodes.InvokeStatic, Opcodes.IfEq, Opcodes.ALoad0, Opcodes.GetField, Opcodes.InvokeVirtual,
                Opcodes.Pop, Opcodes.Nop, Opcodes.Nop, Opcodes.Goto, Opcodes.ALoad0, Opcodes.GetField, Opcodes.InvokeVirtual, Opcodes.InvokeVirtual, Opcodes.Return,
            ],
            OpcodesOf(code)
        );
        Assert.True(patched.Invokes(code.Instructions[index: 3], helper));
        Assert.Equal(
            [Opcodes.ALoad0, Opcodes.InvokeVirtual, Opcodes.ALoad0, Opcodes.InvokeVirtual, Opcodes.IReturn],
            OpcodesOf(patched.FindMethod(helper.Name, helper.Descriptor)?.Code)
        );
        Assert.Equal(expected: 0x100A, patched.FindMethod(helper.Name, helper.Descriptor)?.AccessFlags);
    }

    /// <summary>Once patched, the handler hands the client to the helper, which waits for the client thread, and then runs its old code, moved to a method of its own.</summary>
    /// <param name="className">The listener.</param>
    /// <param name="client">The client's class.</param>
    /// <param name="packet">The Disconnect packet.</param>
    /// <param name="handler">The name of the handler, which Forge from 1.17 to 1.20.4 gives by SRG.</param>
    [Theory]
    [MemberData(nameof(Listeners))]
    public void PatchWaitsForTheClientThreadBeforeTheDisconnect(string className, string client, string packet, string handler)
    {
        string descriptor = "(L" + packet + ";)V";
        ClassPatchResult result = Apply(CreateListener(className, client, packet, handler));
        JavaClassFile patched = JavaClassFile.Read(result.Bytes ?? throw new InvalidOperationException(result.ToString()));
        CodeAttribute code = patched.FindMethod(handler, descriptor)?.Code ?? throw new InvalidOperationException(message: "The handler is gone.");

        Assert.Contains(
            code.Instructions,
            instruction => patched.Invokes(
                instruction,
                JavaMethod.Static(owner: "modloaderfixes/QueuedPackets", name: "awaitHandled", descriptor: "(Ljava/util/concurrent/Executor;)V")
            )
        );
        Assert.Equal(
            [Opcodes.ALoad0, Opcodes.GetField, Opcodes.ALoad1, Opcodes.InvokeVirtual, Opcodes.InvokeVirtual, Opcodes.Return],
            OpcodesOf(patched.FindMethod(Moved, descriptor)?.Code)
        );
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string name = JavaClassFile.Read(classFile).Name;

        return FinalPackets.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile)).Single(result => result.ClassName == name);
    }

    // A controller as Minecraft 1.7 compiles it, whose tick processes the received packets while the connection is open and reports the disconnect otherwise.
    private static byte[] CreateController(string className, string tick, string client, string getManager, string manager, string isOpen, string process)
    {
        JavaMethod getNetworkManager = JavaMethod.Virtual(client, getManager, "()L" + manager + ";");
        JavaMethod isChannelOpen = JavaMethod.Virtual(manager, isOpen, descriptor: "()Z");
        JavaMethod processReceivedPackets = JavaMethod.Virtual(manager, process, descriptor: "()V");
        JavaMethod checkDisconnected = JavaMethod.Virtual(manager, name: "checkDisconnected", descriptor: "()V");
        string handler = "L" + client + ";";

        // The branches need a class of version 50, which the JVM verifies without stack map frames, as Minecraft 1.7's are.
        return new ClassFileWriter(className, superName: "java/lang/Object", accessFlags: 0x0021, majorVersion: 50)
            .AddField(name: "b", handler, accessFlags: 0x0012)
            .AddMethod(
                tick,
                descriptor: "()V",
                code =>
                {
                    CodeLabel disconnected = code.NewLabel();
                    CodeLabel done = code.NewLabel();

                    return code.ALoad(slot: 0)
                        .GetField(className, name: "b", handler)
                        .Invoke(getNetworkManager)
                        .Invoke(isChannelOpen)
                        .If(Opcodes.IfEq, disconnected)
                        .ALoad(slot: 0)
                        .GetField(className, name: "b", handler)
                        .Invoke(getNetworkManager)
                        .Invoke(processReceivedPackets)
                        .Goto(done)
                        .Mark(disconnected)
                        .ALoad(slot: 0)
                        .GetField(className, name: "b", handler)
                        .Invoke(getNetworkManager)
                        .Invoke(checkDisconnected)
                        .Mark(done)
                        .Return();
                },
                accessFlags: 0x0001
            )
            .ToArray();
    }

    // A listener as Minecraft compiles it: a constructor that takes the client first and keeps it in a field, the connection, and the Disconnect's handler, which
    // closes the connection with the packet's reason.
    private static byte[] CreateListener(string className, string client, string packet, string handler)
    {
        string connection = "Lnet/minecraft/network/Connection;";

        return new ClassFileWriter(className, superName: "java/lang/Object", accessFlags: 0x0021, majorVersion: 52)
            .AddField(name: "f", "L" + client + ";", accessFlags: 0x0012)
            .AddField(name: "c", connection, accessFlags: 0x0012)
            .AddMethod(
                name: "<init>",
                "(L" + client + ";" + connection + ")V",
                code => code.ALoad(slot: 0)
                    .InvokeSpecial(owner: "java/lang/Object", name: "<init>", descriptor: "()V")
                    .ALoad(slot: 0)
                    .ALoad(slot: 1)
                    .PutField(className, name: "f", "L" + client + ";")
                    .ALoad(slot: 0)
                    .ALoad(slot: 2)
                    .PutField(className, name: "c", connection)
                    .Return(),
                accessFlags: 0x0001
            )
            .AddMethod(
                handler,
                "(L" + packet + ";)V",
                code => code.ALoad(slot: 0)
                    .GetField(className, name: "c", connection)
                    .ALoad(slot: 1)
                    .Invoke(JavaMethod.Virtual(packet, name: "getReason", descriptor: "()Ljava/lang/String;"))
                    .Invoke(JavaMethod.Virtual(owner: "net/minecraft/network/Connection", name: "disconnect", descriptor: "(Ljava/lang/String;)V"))
                    .Return(),
                accessFlags: 0x0001
            )
            .ToArray();
    }

    private static List<byte> OpcodesOf(CodeAttribute? code)
    {
        return [.. (code ?? throw new InvalidOperationException(message: "The handler's code wasn't moved.")).Instructions.Select(static instruction => instruction.Opcode)];
    }
}
