using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Keeping Minecraft 1.7's outbound packets in order: the send and the flush of NetworkManager synchronized, by Forge's names and by vanilla's shape.</summary>
public sealed class OutboundPacketsPatchTests
{
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Handler = "io/netty/channel/SimpleChannelInboundHandler";
    private const string Listeners = "[Lio/netty/util/concurrent/GenericFutureListener;";
    private const int Synchronized = 0x0020;

    /// <summary>A NetworkManager without the send, by other names, is left alone with a reason that names it.</summary>
    [Fact]
    public void ForgePatchLeavesManagersWithoutTheSend()
    {
        ClassPatchResult result = OutboundPackets.Forge.Apply(
            CreateManager(
                ForgeManager,
                packet: "net/minecraft/network/Packet",
                send: "scheduleOutboundPacket",
                dispatch: "func_150732_b",
                flush: "func_150733_h"
            )
        );

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "func_150725_a", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Forge's send and flush become synchronized, and its dispatch keeps its flags.</summary>
    [Fact]
    public void ForgePatchSynchronizesTheSendAndTheFlush()
    {
        byte[] patched = Patched(
            OutboundPackets.Forge,
            CreateManager(ForgeManager, packet: "net/minecraft/network/Packet", send: "func_150725_a", dispatch: "func_150732_b", flush: "func_150733_h")
        );

        AssertSynchronized(patched, send: "func_150725_a", dispatch: "func_150732_b", flush: "func_150733_h", packet: "net/minecraft/network/Packet");
    }

    /// <summary>Minecraft 1.8 sends with an extra listener, so its manager has no send of 1.7's shape and is left alone.</summary>
    [Fact]
    public void VanillaPatchLeavesMinecraft18()
    {
        ClassPatchResult result = OutboundPackets.Vanilla.Apply(CreateManager(className: "gr", packet: "id", send: null, dispatch: "a", flush: "m"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The vanilla patch only looks at the default package, so Forge's NetworkManager and other handlers don't match it.</summary>
    [Fact]
    public void VanillaPatchOnlyMatchesTheDefaultPackage()
    {
        Assert.False(
            OutboundPackets.Vanilla.Matches(
                CreateManager(ForgeManager, packet: "net/minecraft/network/Packet", send: "func_150725_a", dispatch: "func_150732_b", flush: "func_150733_h"),
                hierarchy: null
            )
        );
    }

    /// <summary>Vanilla 1.7's manager, found by its shape under any name, gets its send and flush synchronized, and its dispatch keeps its flags.</summary>
    [Fact]
    public void VanillaPatchSynchronizesTheSendAndTheFlushByTheirShape()
    {
        byte[] classFile = CreateManager(className: "ej", packet: "ft", send: "a", dispatch: "b", flush: "i");

        Assert.True(OutboundPackets.Vanilla.Matches(classFile, hierarchy: null));

        AssertSynchronized(Patched(OutboundPackets.Vanilla, classFile), send: "a", dispatch: "b", flush: "i", packet: "ft");
    }

    private static void AssertSynchronized(byte[] patched, string send, string dispatch, string flush, string packet)
    {
        JavaClassFile model = JavaClassFile.Read(patched);
        string descriptor = "(L" + packet + ";" + Listeners + ")V";

        Assert.Equal(Synchronized, Flags(model, send, descriptor) & Synchronized);
        Assert.Equal(Synchronized, Flags(model, flush, descriptor: "()V") & Synchronized);
        Assert.Equal(expected: 0, Flags(model, dispatch, descriptor) & Synchronized);
    }

    // A manager with 1.7's send, which flushes the queue and dispatches, a private dispatch and a flush that dispatches; without a send, it has 1.8's instead, which
    // takes an extra listener.
    private static byte[] CreateManager(string className, string packet, string? send, string dispatch, string flush)
    {
        string descriptor = "(L" + packet + ";" + Listeners + ")V";
        JavaMethod dispatching = JavaMethod.Virtual(className, dispatch, descriptor);
        JavaMethod flushing = JavaMethod.Virtual(className, flush, descriptor: "()V");

        return new ClassFileWriter(className, Handler, majorVersion: 50)
            .AddMethod(
                send ?? "a",
                send is null ? "(L" + packet + ";Lio/netty/util/concurrent/GenericFutureListener;" + Listeners + ")V" : descriptor,
                code => code.ALoad(slot: 0).Invoke(flushing).ALoad(slot: 0).ALoad(slot: 1).ALoad(slot: 2).Invoke(dispatching).Return(),
                accessFlags: 0x0081
            )
            .AddMethod(dispatch, descriptor, code => code.Return(), accessFlags: 0x0002)
            .AddMethod(
                flush,
                descriptor: "()V",
                code => code.ALoad(slot: 0).AConstNull().AConstNull().Invoke(dispatching).Return(),
                accessFlags: 0x0002
            )
            .ToArray();
    }

    private static int Flags(JavaClassFile model, string name, string descriptor)
    {
        return (model.FindMethod(name, descriptor) ?? throw new InvalidOperationException($"{model.Name} has no {name}{descriptor}.")).AccessFlags;
    }

    private static byte[] Patched(ClassPatch patch, byte[] classFile)
    {
        ClassPatchResult result = patch.Apply(classFile);

        return result.Bytes ?? throw new InvalidOperationException(result.ToString());
    }
}
