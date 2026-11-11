using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Lets a Forge 1.7 client's network thread wait for the client thread as long as the server would keep the connection. Once the server says the login succeeded,
/// FML's handshake waits in <c>FMLClientHandler.waitForPlayClient</c> for the client thread's next network tick to set the play handler up, and gives the connection
/// up with <c>Timeout waiting for client thread to catch up!</c> after one second in Forge for 1.7.2 and five in 1.7.10. A client thread that is still busy, as it is
/// on a loaded machine or right after a previous connection ended, drops a join that nothing is wrong with. <c>connectToServer</c> also replaces the latch only
/// after the new connection has started, so a server that answers first lets the wait return at once on the previous connection's latch, which is counted down
/// already, or throw a <c>NullPointerException</c> on the first connection's, which isn't there yet; the handshake then takes the previous connection's play
/// client and casts the login handler still on the connection to <c>NetHandlerPlayClient</c>. After a login the server refused, the previous latch is never counted
/// down, and the wait gives the join up with the same timeout.
/// </summary>
internal static class PlayClientWait
{
    private const string Dispatcher = "cpw/mods/fml/common/network/handshake/NetworkDispatcher";

    private const string FmlClientHandler = "cpw/mods/fml/client/FMLClientHandler";

    private static readonly JavaMethod WaitForPlayClient = JavaMethod.Virtual(owner: "cpw/mods/fml/common/FMLCommonHandler", name: "waitForPlayClient", descriptor: "()V");

    /// <summary>
    /// Gets the patch of <c>FMLClientHandler</c>: <c>waitForPlayClient</c>'s wait on its latch goes to <c>modloaderfixes.PlayClients</c>, which waits up to the
    /// server's read timeout, through the getter of the private <c>playClientBlock</c> that the patch adds. Once the latch is replaced, which the connection's own
    /// <c>connectToServer</c> does when the server answers first, and before the first connection has a latch, it returns and leaves the wait to
    /// <see cref="Handshake"/>, which tells the connection's own play handler apart.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(FmlClientHandler)
        .AddFieldGetter(field: "playClientBlock", name: "modloaderfixes$playClientBlock")
        .Method(name: "waitForPlayClient", descriptor: "()V")
        .RedirectInvocation(
            JavaMethod.Virtual(owner: "java/util/concurrent/CountDownLatch", name: "await", descriptor: "(JLjava/util/concurrent/TimeUnit;)Z"),
            toOwner: "modloaderfixes/PlayClients"
        );

    /// <summary>
    /// Gets the patch of <c>NetworkDispatcher</c>, from Forge 1.7.2-10.12.0.1000, whose <c>clientListenForServerHandshake</c> waits for the play client: after it
    /// returns, <c>modloaderfixes.PlayHandlers</c> waits until the dispatcher's own connection has its play handler and stores it as the dispatcher's, through the
    /// setter of the private <c>netHandler</c> that the patch adds, and lets go of a connection that the client has closed, as its connecting screen's cancel does.
    /// </summary>
    internal static ClassPatch Handshake { get; } = ClassPatch.For(Dispatcher)
        .AddFieldSetter(field: "netHandler", name: "modloaderfixes$netHandler")
        .Method(name: "clientListenForServerHandshake", descriptor: "()V")
        .When(static body => body.Calls(WaitForPlayClient))
        .After(JavaMethod.Static(owner: "modloaderfixes/PlayHandlers", name: "listenedForServerHandshake", "(L" + Dispatcher + ";)V"));
}
