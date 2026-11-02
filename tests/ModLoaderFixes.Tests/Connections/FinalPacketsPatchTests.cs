using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace ModLoaderFixes.Tests.Connections;

/// <summary>Making a Minecraft client's handler of a Disconnect wait until the client thread has handled the packets that came before it.</summary>
public sealed class FinalPacketsPatchTests
{
    private const string Moved = "modloaderfixes$handleDisconnect";

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
