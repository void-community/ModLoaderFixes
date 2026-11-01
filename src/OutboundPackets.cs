using JvmBridge.Agents;

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

    /// <summary>Gets the patch of Forge 1.7's <c>NetworkManager</c>, by FML's names: <c>scheduleOutboundPacket</c> and <c>flushOutboundQueue</c>.</summary>
    internal static ClassPatch Forge { get; } = ClassPatch.For(className: "net/minecraft/network/NetworkManager")
        .Edit(
            Description,
            static context => PacketQueue.Synchronize(
                context.Editor,
                ("func_150725_a", "(Lnet/minecraft/network/Packet;[Lio/netty/util/concurrent/GenericFutureListener;)V"),
                ("func_150733_h", "()V")
            )
        );

    /// <summary>Gets the patch of vanilla 1.7's <c>NetworkManager</c>, which <see cref="PacketQueue"/> finds by its shape, because vanilla's names change in every release.</summary>
    internal static ClassPatch Vanilla { get; } = ClassPatch.ForMatching(static name => !name.Contains(value: '/', StringComparison.Ordinal), description: "classes of the default package")
        .WhereSubclassOf(superType: "io/netty/channel/SimpleChannelInboundHandler")
        .Edit(Description, PacketQueue.SynchronizeByShape);

    /// <summary>Gets the patches, Forge's and vanilla's.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [Forge, Vanilla];
}
