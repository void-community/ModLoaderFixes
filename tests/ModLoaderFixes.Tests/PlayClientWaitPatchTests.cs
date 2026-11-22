using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>
/// Pointing FML 1.7's wait for the play client at the helper that waits as long as the server would keep the connection, and making the handshake wait for its own
/// connection's play handler after it.
/// </summary>
public sealed class PlayClientWaitPatchTests
{
    // The latch's wait with a time limit, which the patch of the client handler sends to its helper.
    private const string Await = "await";
    private const string AwaitDescriptor = "(JLjava/util/concurrent/TimeUnit;)Z";

    // The two classes of FML 1.7 that the patches change: the client handler, which waits for the play client, and the dispatcher of the handshake.
    private const string ClassName = "cpw/mods/fml/client/FMLClientHandler";
    private const string Dispatcher = "cpw/mods/fml/common/network/handshake/NetworkDispatcher";

    // The class of the latch that the handler waits on, the dispatcher's method that listens for the server's handshake, and the name of the wait for the play
    // client, in the handler and in the call that the dispatcher makes.
    private const string Latch = "java/util/concurrent/CountDownLatch";
    private const string Listen = "clientListenForServerHandshake";
    private const string WaitForPlayClient = "waitForPlayClient";

    /// <summary>The dispatcher of Forge 1.7.2 before 10.12.0.1000, which reads the play client without waiting for it, is left alone with a reason.</summary>
    [Fact]
    public void HandshakePatchLeavesADispatcherThatDoesntWait()
    {
        ClassPatchResult skipped = PlayClientWait.Handshake.Apply(CreateDispatcher(waits: false));

        Assert.False(skipped.Changed);
        Assert.Contains(WaitForPlayClient, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// After NetworkDispatcher's wait for the play client, the dispatcher hands itself to the helper, which waits for its connection's play handler, and the patch
    /// adds the setter of netHandler that the helper stores it with.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a NetworkDispatcher whose <c>clientListenForServerHandshake</c> waits for the play client unchanged, so it has no bytes to check; the message
    /// is the patch's own line on why.
    /// </exception>
    [Fact]
    public void HandshakePatchWaitsForTheConnectionsPlayHandler()
    {
        ClassPatchResult applied = PlayClientWait.Handshake.Apply(CreateDispatcher(waits: true));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.AdvisedMethods);

        ClassFileAssert.Calls(patched,
            Listen,
            owner: "modloaderfixes/PlayHandlers",
            name: "listenedForServerHandshake",
            "(L" + Dispatcher + ";)V");

        Assert.NotNull(JavaClassFile.Read(patched)
            .FindMethod(name: "modloaderfixes$netHandler", descriptor: "(Lnet/minecraft/network/INetHandler;)V"));
    }

    /// <summary>A FMLClientHandler without the wait, as Forge for 1.6.4 has, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesHandlersWithoutTheWait()
    {
        ClassPatchResult skipped = PlayClientWait.Patch.Apply(CreateClassFile(method: "connectToServer"));

        Assert.False(skipped.Changed);
        Assert.Contains(WaitForPlayClient, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The wait on the latch goes to the helper, with the latch as its first argument, and the patch adds the getter of playClientBlock that the helper reads.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a FMLClientHandler whose <c>waitForPlayClient</c> waits on a latch unchanged, so it has no bytes to check; the message is the patch's own line
    /// on why.
    /// </exception>
    [Fact]
    public void PatchRedirectsTheWaitAndAddsTheGetter()
    {
        ClassPatchResult applied = PlayClientWait.Patch.Apply(CreateClassFile(WaitForPlayClient));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        Assert.Equal(expected: 1, applied.AddedMembers);

        ClassFileAssert.Calls(patched,
            WaitForPlayClient,
            owner: "modloaderfixes/PlayClients",
            Await,
            descriptor: "(Ljava/util/concurrent/CountDownLatch;JLjava/util/concurrent/TimeUnit;)Z");

        ClassFileAssert.DoesNotCall(patched, WaitForPlayClient, Latch, Await, AwaitDescriptor);

        Assert.NotNull(JavaClassFile.Read(patched)
            .FindMethod(name: "modloaderfixes$playClientBlock", descriptor: "()Ljava/util/concurrent/CountDownLatch;"));
    }

    // A FMLClientHandler with the private playClientBlock, whose method waits on a null latch for one null unit, as waitForPlayClient waits on playClientBlock.
    private static byte[] CreateClassFile(string method)
    {
        JavaMethod await = JavaMethod.Virtual(Latch, Await, AwaitDescriptor);

        return new ClassFileWriter(ClassName).AddField(name: "playClientBlock", descriptor: "Ljava/util/concurrent/CountDownLatch;")
            .AddMethod(method, descriptor: "()V", code => code.AConstNull().LConst(value: 1).AConstNull().Invoke(await).Pop().Return())
            .ToArray();
    }

    // A NetworkDispatcher with the private netHandler, whose clientListenForServerHandshake waits for the play client through FMLCommonHandler or doesn't.
    private static byte[] CreateDispatcher(bool waits)
    {
        JavaMethod wait = JavaMethod.Virtual(owner: "cpw/mods/fml/common/FMLCommonHandler",
            WaitForPlayClient,
            descriptor: "()V");

        return new ClassFileWriter(Dispatcher).AddField(name: "netHandler", descriptor: "Lnet/minecraft/network/INetHandler;")
            .AddMethod(Listen, descriptor: "()V", code => (waits ? code.AConstNull().Invoke(wait) : code).Return())
            .ToArray();
    }
}
