using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Redirecting the registry rebuilds of FML's client handshake states to the helper that runs them on the client thread.</summary>
public sealed class RegistryThreadPatchTests
{
    private const string Accept = "accept";
    private const string ClientThread = "modloaderfixes/ClientThread";
    private const string Lambda = "lambda$accept$0";

    /// <summary>Forge for Minecraft 1.12.2 already runs the injection from a lambda on the client thread, so a WAITINGSERVERCOMPLETE that only calls it there is left alone.</summary>
    [Fact]
    public void InjectionPatchLeavesAnInjectionThatForgeAlreadyScheduled()
    {
        ClassPatchResult skipped = PatcherAgent.RegistryInjection
            .Apply(CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4", PatcherAgent.InjectSnapshot, Lambda));

        Assert.False(skipped.Changed);

        Assert.Contains("no invocation of " + PatcherAgent.InjectSnapshot.Owner,
            skipped.SkippedReason,
            StringComparison.Ordinal);
    }

    /// <summary>The injection in WAITINGSERVERCOMPLETE's <c>accept</c> now goes to the helper, which returns its result.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a WAITINGSERVERCOMPLETE whose <c>accept</c> calls the injection itself unchanged, so it has no bytes to check; the message is the patch's own
    /// line on why.
    /// </exception>
    [Fact]
    public void InjectionPatchRedirectsTheInjectionInAccept()
    {
        ClassPatchResult applied = PatcherAgent.RegistryInjection
            .Apply(CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4", PatcherAgent.InjectSnapshot, Accept));

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);

        ClassFileAssert.Calls(patched,
            Accept,
            ClientThread,
            PatcherAgent.InjectSnapshot.Name,
            PatcherAgent.InjectSnapshot.Descriptor);

        ClassFileAssert.DoesNotCall(patched,
            Accept,
            PatcherAgent.InjectSnapshot.Owner,
            PatcherAgent.InjectSnapshot.Name,
            PatcherAgent.InjectSnapshot.Descriptor);
    }

    /// <summary>Forge for Minecraft 1.12.2 already runs the revert from a lambda on the client thread, so a DONE that only calls it there is left alone.</summary>
    [Fact]
    public void RevertPatchLeavesARevertThatForgeAlreadyScheduled()
    {
        ClassPatchResult skipped = PatcherAgent.RegistryRevert
            .Apply(CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7", PatcherAgent.RevertToFrozen, Lambda));

        Assert.False(skipped.Changed);

        Assert.Contains("no invocation of " + PatcherAgent.RevertToFrozen.Owner,
            skipped.SkippedReason,
            StringComparison.Ordinal);
    }

    /// <summary>The revert in DONE's <c>accept</c> now goes to the helper.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a DONE whose <c>accept</c> calls the revert itself unchanged, so it has no bytes to check; the message is the patch's own line on why.
    /// </exception>
    [Fact]
    public void RevertPatchRedirectsTheRevertInAccept()
    {
        ClassPatchResult applied = PatcherAgent.RegistryRevert
            .Apply(CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7", PatcherAgent.RevertToFrozen, Accept));

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);

        ClassFileAssert.Calls(patched,
            Accept,
            ClientThread,
            PatcherAgent.RevertToFrozen.Name,
            PatcherAgent.RevertToFrozen.Descriptor);

        ClassFileAssert.DoesNotCall(patched,
            Accept,
            PatcherAgent.RevertToFrozen.Owner,
            PatcherAgent.RevertToFrozen.Name,
            PatcherAgent.RevertToFrozen.Descriptor);
    }

    // A class file with an empty accept and a method that calls the given GameData method with null and false arguments and returns what it returns.
    private static byte[] CreateClassFile(string className, JavaMethod call, string caller)
    {
        bool returnsValue = !call.Descriptor.EndsWith(value: ")V", StringComparison.Ordinal);
        bool takesArguments = !call.Descriptor.StartsWith(value: "()", StringComparison.Ordinal);
        ClassFileWriter writer = new(className);

        if (caller != Accept)
            writer = writer.AddMethod(Accept, descriptor: "()V", static code => code.Return());

        return writer
            .AddMethod(caller,
                returnsValue ? "()Ljava/lang/Object;" : "()V",
                code =>
                {
                    CodeBuilder called = (takesArguments ? code.AConstNull().IConst(value: 0).IConst(value: 0) : code).Invoke(call);

                    return returnsValue ? called.AReturn() : called.Return();
                })
            .ToArray();
    }
}
