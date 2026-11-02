using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Keeps Minecraft 1.7's network tick from dropping a connection that it sees before the connection has a channel, as Minecraft 1.8 does. The channel initializer
/// adds each new <c>NetworkManager</c> to the list that the server thread ticks, and only <c>channelActive</c> gives it its channel afterwards. A tick in between
/// takes the manager for a closed connection: it drops it from the list and tells its handshake handler, which does nothing, so the login that the network thread
/// goes on to start is never answered, never times out and never notices the client leave. Forge, PaperSpigot and vanilla servers each run that code under other
/// names, so each has its own patch.
/// </summary>
internal static class PendingConnections
{
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Helper = "modloaderfixes/Connections";
    private const string PaperManager = "net/minecraft/server/v1_7_R4/NetworkManager";

    /// <summary>
    /// Gets the patch of Forge 1.7's <c>NetworkSystem.networkTick</c>, by FML's names: its check whether a manager's channel is open, and its processing of the
    /// manager's packets, go to <c>modloaderfixes.Connections</c>, which counts a manager without a channel as open and skips its processing until it has one.
    /// Reporting it open alone would not do, because the processing reads the channel at once.
    /// </summary>
    internal static ClassPatch Forge { get; } = ClassPatch.For(className: "net/minecraft/network/NetworkSystem")
        .Method(name: "func_151269_c", descriptor: "()V")
        .RedirectInvocation(JavaMethod.Virtual(ForgeManager, name: "func_150724_d", descriptor: "()Z"), Helper, toName: "isOpenOrPending")
        .RedirectInvocation(JavaMethod.Virtual(ForgeManager, name: "func_74428_b", descriptor: "()V"), Helper, toName: "processIfActive");

    /// <summary>
    /// Gets the patch of PaperSpigot 1.7.10's <c>ServerConnection.c</c>, the same tick by CraftBukkit's names, with the same redirects as <see cref="Forge"/>. Spigot
    /// skips a manager whose <c>preparing</c> flag is still set, but reads the flag after it found the channel missing, so a <c>channelActive</c> between the two reads
    /// still drops the manager; the helper decides on one read of the channel.
    /// </summary>
    internal static ClassPatch Paper { get; } = ClassPatch.For(className: "net/minecraft/server/v1_7_R4/ServerConnection")
        .Method(name: "c", descriptor: "()V")
        .RedirectInvocation(JavaMethod.Virtual(PaperManager, name: "isConnected", descriptor: "()Z"), Helper, toName: "isOpenOrPending")
        .RedirectInvocation(JavaMethod.Virtual(PaperManager, name: "a", descriptor: "()V"), Helper, toName: "processIfActive");

    /// <summary>
    /// Gets the patch of PaperSpigot 1.7.10's <c>NetworkManager</c>, which adds the getter of its private channel that the helper reads. It returns an Object,
    /// because PaperSpigot relocates Netty, so the helper doesn't name its Channel.
    /// </summary>
    internal static ClassPatch PaperChannel { get; } = ClassPatch.For(PaperManager)
        .AddFieldGetter(field: "m", name: "modloaderfixes$channel", returns: "Ljava/lang/Object;");

    /// <summary>
    /// Gets the patch of vanilla 1.7's channel initializers, for the network and, in a client's integrated server, in memory, under each release's names, which
    /// <see cref="VanillaInitializer"/> checks by what their <c>initChannel</c> does. The tick's names change in every release too, and a helper can't call members
    /// whose names it doesn't know, so this fixes the other end: the initializer's adding of the manager to the tick's list goes to
    /// <c>modloaderfixes.NewConnections</c>, which adds it from a task on the channel's event loop, and Netty runs that task after channelActive.
    /// </summary>
    internal static ClassPatch Vanilla { get; } = ClassPatch.ForMatching(VanillaInitializer.IsInitializer, description: "vanilla 1.7's channel initializers")
        .WhereSubclassOf(superType: "io/netty/channel/ChannelInitializer")
        .Method(VanillaInitializer.MethodName, VanillaInitializer.MethodDescriptor)
        .Edit(description: "new connections added to the network tick once their channel is active", VanillaInitializer.Defer);

    /// <summary>Gets the patches, one per server that runs the tick.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [Forge, Paper, PaperChannel, Vanilla];
}
