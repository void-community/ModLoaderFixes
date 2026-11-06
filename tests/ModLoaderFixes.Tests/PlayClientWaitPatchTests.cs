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
    private const string Await = "await";
    private const string AwaitDescriptor = "(JLjava/util/concurrent/TimeUnit;)Z";
    private const string ClassName = "cpw/mods/fml/client/FMLClientHandler";
    private const string Dispatcher = "cpw/mods/fml/common/network/handshake/NetworkDispatcher";
    private const string Latch = "java/util/concurrent/CountDownLatch";
    private const string Listen = "clientListenForServerHandshake";
    private const string WaitForPlayClient = "waitForPlayClient";

    /// <summary>The dispatcher of Forge 1.7.2 before 10.12.0.1000, which reads the play client without waiting for it, is left alone with a reason.</summary>
    [Fact]
    public void HandshakePatchLeavesADispatcherThatDoesntWait()
    {
        ClassPatchResult result = PlayClientWait.Handshake.Apply(CreateDispatcher(waits: false));

        Assert.False(result.Changed);
        Assert.Contains(WaitForPlayClient, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// After NetworkDispatcher's wait for the play client, the dispatcher hands itself to the helper, which waits for its connection's play handler, and the patch
    /// adds the setter of netHandler that the helper stores it with.
    /// </summary>
    [Fact]
    public void HandshakePatchWaitsForTheConnectionsPlayHandler()
    {
        ClassPatchResult result = PlayClientWait.Handshake.Apply(CreateDispatcher(waits: true));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AdvisedMethods);
        ClassFileAssert.Calls(patched, Listen, owner: "modloaderfixes/PlayHandlers", name: "listenedForServerHandshake", "(L" + Dispatcher + ";)V");
        Assert.NotNull(JavaClassFile.Read(patched).FindMethod(name: "modloaderfixes$netHandler", descriptor: "(Lnet/minecraft/network/INetHandler;)V"));
    }

    /// <summary>A FMLClientHandler without the wait, as Forge for 1.6.4 has, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesHandlersWithoutTheWait()
    {
        ClassPatchResult result = PlayClientWait.Patch.Apply(CreateClassFile(method: "connectToServer"));

        Assert.False(result.Changed);
        Assert.Contains(WaitForPlayClient, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The wait on the latch goes to the helper, with the latch as its first argument, and the patch adds the getter of playClientBlock that the helper reads.</summary>
    [Fact]
    public void PatchRedirectsTheWaitAndAddsTheGetter()
    {
        ClassPatchResult result = PlayClientWait.Patch.Apply(CreateClassFile(WaitForPlayClient));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        Assert.Equal(expected: 1, result.AddedMembers);
        ClassFileAssert.Calls(
            patched,
            WaitForPlayClient,
            owner: "modloaderfixes/PlayClients",
            Await,
            descriptor: "(Ljava/util/concurrent/CountDownLatch;JLjava/util/concurrent/TimeUnit;)Z"
        );
        ClassFileAssert.DoesNotCall(patched, WaitForPlayClient, Latch, Await, AwaitDescriptor);
        Assert.NotNull(
            JavaClassFile.Read(patched).FindMethod(name: "modloaderfixes$playClientBlock", descriptor: "()Ljava/util/concurrent/CountDownLatch;")
        );
    }

    // A FMLClientHandler with the private playClientBlock, whose method waits on a null latch for one null unit, as waitForPlayClient waits on playClientBlock.
    private static byte[] CreateClassFile(string method)
    {
        JavaMethod await = JavaMethod.Virtual(Latch, Await, AwaitDescriptor);

        return new ClassFileWriter(ClassName)
            .AddField(name: "playClientBlock", descriptor: "Ljava/util/concurrent/CountDownLatch;")
            .AddMethod(method, descriptor: "()V", code => code.AConstNull().LConst(value: 1).AConstNull().Invoke(await).Pop().Return())
            .ToArray();
    }

    // A NetworkDispatcher with the private netHandler, whose clientListenForServerHandshake waits for the play client through FMLCommonHandler or doesn't.
    private static byte[] CreateDispatcher(bool waits)
    {
        JavaMethod wait = JavaMethod.Virtual(owner: "cpw/mods/fml/common/FMLCommonHandler", WaitForPlayClient, descriptor: "()V");

        return new ClassFileWriter(Dispatcher)
            .AddField(name: "netHandler", descriptor: "Lnet/minecraft/network/INetHandler;")
            .AddMethod(Listen, descriptor: "()V", code => (waits ? code.AConstNull().Invoke(wait) : code).Return())
            .ToArray();
    }
}
