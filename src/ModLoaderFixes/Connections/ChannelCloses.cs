using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Connections;

/// <summary>
/// Makes a Minecraft 1.7.2 to 1.8 connection send what it still has to send before it closes, so that a kicked player gets the reason. Those releases set
/// <c>TCP_NODELAY</c> to false, so the kernel holds a small packet back while an earlier one is unacknowledged. A kick writes its Disconnect packet, closes the
/// channel as soon as the write completes, and stops reading, so the player's next packets stay unread. Java before 11 closes the socket at once, and Linux
/// resets a connection that is closed with input unread and throws away what it hadn't sent: the Disconnect packet never leaves, and the player sees the
/// connection reset. Minecraft 1.8.1 sets <c>TCP_NODELAY</c> to true, and Java from 11 shuts the output of such a channel down before it closes it, which sends
/// what is held. The patches point the <c>Channel.close()</c> in <c>NetworkManager.closeChannel</c> at <c>modloaderfixes.ChannelCloses</c>, which shuts the
/// output of a socket with Nagle's algorithm on down first and then closes the channel. Forge runs that method under the same names up to 1.8.9, whose
/// sockets have the algorithm off, and the helper closes those as before.
/// </summary>
internal static class ChannelCloses
{
    private const string Helper = "modloaderfixes/ChannelCloses";
    private const string PaperHelper = "modloaderfixes/PaperChannelCloses";
    private const string PaperNetty = "net/minecraft/util/io/netty/channel/";

    private static readonly JavaMethod Close = JavaMethod.Interface(owner: "io/netty/channel/Channel", name: "close", descriptor: "()Lio/netty/channel/ChannelFuture;");

    /// <summary>
    /// Gets the patch of PaperSpigot 1.7.10's <c>NetworkManager.close</c>, the same method by CraftBukkit's names, whose channel is of the package that CraftBukkit
    /// relocates Netty to, so it has a helper of its own.
    /// </summary>
    internal static ClassPatch Paper { get; } = ClassPatch.For(className: "net/minecraft/server/v1_7_R4/NetworkManager")
        .Method(name: "close", descriptor: "(Lnet/minecraft/server/v1_7_R4/IChatBaseComponent;)V")
        .RedirectInvocation(JavaMethod.Interface(PaperNetty + "Channel", name: "close", "()L" + PaperNetty + "ChannelFuture;"), PaperHelper);

    /// <summary>
    /// Gets the patches of <c>closeChannel</c>: Forge's, by FML's names, vanilla's under each release's names from 1.7.2 to 1.8, and PaperSpigot 1.7.10's.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [.. MappedClasses.ClosingNetworkManager.Patches(Redirect), Paper];

    // A class that lacks closeChannel under the naming's names, as another release's class of the same name does, has no such call and is left alone.
    private static ClassPatch Redirect(ClassNames names)
    {
        JavaMethod closeChannel = names.Virtual(name: "func_150718_a", descriptor: "(Lnet/minecraft/util/IChatComponent;)V");

        return names.Patch().Method(closeChannel.Name, closeChannel.Descriptor).RedirectInvocation(Close, Helper);
    }
}
