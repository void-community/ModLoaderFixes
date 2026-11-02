using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Keeping Minecraft 1.7's outbound packets in order: the send and the flush of NetworkManager synchronized, under Forge's names and each vanilla release's.</summary>
public sealed class OutboundPacketsPatchTests
{
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Handler = "io/netty/channel/SimpleChannelInboundHandler";
    private const string Listeners = "[Lio/netty/util/concurrent/GenericFutureListener;";
    private const int Synchronized = 0x0020;

    /// <summary>Gets each vanilla naming of the manager: its name, its packet's, and its flush's, which 1.7.8 renamed.</summary>
    public static TheoryData<string, string, string> VanillaManagers { get; } = new()
    {
        { "ef", "fk", "h" },
        { "eg", "fq", "h" },
        { "ej", "ft", "h" },
        { "ej", "ft", "i" },
    };

    /// <summary>A NetworkManager without the send, by other names, is left alone with a reason that names it.</summary>
    [Fact]
    public void ForgePatchLeavesManagersWithoutTheSend()
    {
        ClassPatchResult result = OutboundPackets.Patches[index: 0]
            .Apply(
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
        byte[] classFile = CreateManager(ForgeManager, packet: "net/minecraft/network/Packet", send: "func_150725_a", dispatch: "func_150732_b", flush: "func_150733_h");

        AssertSynchronized(
            Patched(classFile),
            send: "func_150725_a",
            dispatch: "func_150732_b",
            flush: "func_150733_h",
            packet: "net/minecraft/network/Packet"
        );
    }

    /// <summary>Only the names that the 1.7 releases give their managers match, so another obfuscated class of the manager's shape is left alone.</summary>
    [Fact]
    public void PatchesOnlyMatchTheNamesOfTheManagers()
    {
        byte[] classFile = CreateManager(className: "gr", packet: "ft", send: "a", dispatch: "b", flush: "i");

        Assert.DoesNotContain(OutboundPackets.Patches, patch => patch.Matches(classFile, hierarchy: null));
    }

    /// <summary>
    /// Minecraft 1.8 sends with an extra listener, so a class of its shape is left alone, even under a name that a 1.7 release gives its manager; the patches that
    /// match it skip it with the method they miss.
    /// </summary>
    [Fact]
    public void VanillaPatchesLeaveMinecraft18()
    {
        byte[] classFile = CreateManager(className: "ej", packet: "ft", send: null, dispatch: "b", flush: "i");
        ClassPatchResult[] results = [.. OutboundPackets.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile))];

        Assert.Equal(expected: 2, results.Length);
        Assert.All(results, result => Assert.Contains(expectedSubstring: "ej has no a(Lft;", result.SkippedReason, StringComparison.Ordinal));
    }

    /// <summary>Each vanilla release's manager gets its send and flush synchronized by the one patch of its naming, and its dispatch keeps its flags.</summary>
    /// <param name="manager">The manager's obfuscated name.</param>
    /// <param name="packet">The packet's.</param>
    /// <param name="flush">The flush's.</param>
    [Theory]
    [MemberData(nameof(VanillaManagers))]
    public void VanillaPatchesSynchronizeTheSendAndTheFlushOfEachRelease(string manager, string packet, string flush)
    {
        AssertSynchronized(Patched(CreateManager(manager, packet, send: "a", dispatch: "b", flush)), send: "a", dispatch: "b", flush, packet);
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

    // The class as the one patch that matches and changes it leaves it, as the agent applies the patches.
    private static byte[] Patched(byte[] classFile)
    {
        ClassPatchResult[] changes =
        [
            .. OutboundPackets.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile)).Where(static result => result.Changed),
        ];

        return Assert.Single(changes).Bytes ?? throw new InvalidOperationException(changes[0].ToString());
    }
}
