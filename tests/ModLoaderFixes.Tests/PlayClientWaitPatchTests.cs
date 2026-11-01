using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Pointing FML 1.7's wait for the play client at the helper that waits as long as the server would keep the connection.</summary>
public sealed class PlayClientWaitPatchTests
{
    private const string Await = "await";
    private const string AwaitDescriptor = "(JLjava/util/concurrent/TimeUnit;)Z";
    private const string ClassName = "cpw/mods/fml/client/FMLClientHandler";
    private const string Latch = "java/util/concurrent/CountDownLatch";
    private const string WaitForPlayClient = "waitForPlayClient";

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
}
