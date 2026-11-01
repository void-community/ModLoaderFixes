using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Lets a Forge 1.7 client's network thread wait for the client thread as long as the server would keep the connection. Once the server says the login succeeded,
/// FML's handshake waits in <c>FMLClientHandler.waitForPlayClient</c> for the client thread's next network tick to set the play handler up, and gives the connection
/// up with <c>Timeout waiting for client thread to catch up!</c> after one second in Forge for 1.7.2 and five in 1.7.10. A client thread that is still busy, as it is
/// on a loaded machine or right after a previous connection ended, drops a join that nothing is wrong with.
/// </summary>
internal static class PlayClientWait
{
    /// <summary>
    /// Gets the patch of <c>FMLClientHandler</c>: <c>waitForPlayClient</c>'s wait on its latch goes to <c>modloaderfixes.PlayClients</c>, which waits up to the
    /// server's read timeout and stops early once a new connection has replaced the latch, through the getter of the private <c>playClientBlock</c> that the
    /// patch adds.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "cpw/mods/fml/client/FMLClientHandler")
        .AddFieldGetter(field: "playClientBlock", name: "modloaderfixes$playClientBlock")
        .Method(name: "waitForPlayClient", descriptor: "()V")
        .RedirectInvocation(
            JavaMethod.Virtual(owner: "java/util/concurrent/CountDownLatch", name: "await", descriptor: "(JLjava/util/concurrent/TimeUnit;)Z"),
            toOwner: "modloaderfixes/PlayClients"
        );
}
