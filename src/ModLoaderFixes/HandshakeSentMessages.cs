using JvmBridge.Agents;

namespace ModLoaderFixes;

/// <summary>
/// Makes the list of login messages that Forge's handshake handler waits on a <c>CopyOnWriteArrayList</c>. The server thread adds to it while the network thread
/// removes the messages the client answers with <c>removeIf</c>, which throws <c>ConcurrentModificationException</c> on an <c>ArrayList</c> that changes meanwhile,
/// and the connection closes with it. Forge for Minecraft 1.13 to 1.20.1, and NeoForge for 1.20.1, create the list in the handler's constructor. NeoForge for 1.20.2
/// to 1.20.4-20.4.69-beta keeps the indexes in a fastutil <c>IntArrayList</c> instead, where a removal during an add loses the add, and the handshake ends before
/// the client's last answer.
/// </summary>
internal static class HandshakeSentMessages
{
    /// <summary>Gets the patch, which matches each name the handler has had: in <c>fml.network</c> up to 1.16, in <c>fmllegacy.network</c> in 1.17 and in <c>network</c> from 1.18.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.ForMatching(
            static className => className is "net/minecraftforge/fml/network/FMLHandshakeHandler" or "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler" or "net/minecraftforge/network/HandshakeHandler",
            description: "Forge's handshake handler"
        )
        .Method(name: "<init>")
        // Every other ArrayList the constructor creates stays one.
        .ReplaceConstruction(fromClass: "java/util/ArrayList", toClass: "java/util/concurrent/CopyOnWriteArrayList", storedIn: "sentMessages");

    /// <summary>Gets the patch of NeoForge's handler from 1.20.2, whose <c>IntArrayList</c> becomes <c>modloaderfixes.HandshakeIndexes</c>, which locks each method.</summary>
    internal static ClassPatch NeoForgePatch { get; } = ClassPatch.For(className: "net/neoforged/neoforge/network/HandshakeHandler")
        .Method(name: "<init>")
        .ReplaceConstruction(fromClass: "it/unimi/dsi/fastutil/ints/IntArrayList", toClass: "modloaderfixes/HandshakeIndexes", storedIn: "sentMessages");
}
