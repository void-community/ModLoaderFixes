using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>One flush of NetworkManager's outbound queue at a time, from Minecraft 1.7.2 to 1.14.3: the flush synchronized, under Forge's names and each vanilla release's.</summary>
public sealed class OutboundPacketsPatchTests
{
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Handler = "io/netty/channel/SimpleChannelInboundHandler";
    private const string SendDescriptor = "(Ljava/lang/Object;)V";
    private const int Synchronized = 0x0020;

    private static readonly JavaMethod IsEmpty = JavaMethod.Interface(owner: "java/util/Queue", name: "isEmpty", descriptor: "()Z");
    private static readonly JavaMethod Poll = JavaMethod.Interface(owner: "java/util/Queue", name: "poll", descriptor: "()Ljava/lang/Object;");

    /// <summary>Gets vanilla's managers at the edges of their namings: 1.7.2, 1.7.10, 1.8, 1.9.3, 1.12.2 and 1.14.3, each with its flush's name.</summary>
    public static TheoryData<string, string> VanillaManagers { get; } = new()
    {
        { "ef", "h" },
        { "ej", "i" },
        { "gr", "m" },
        { "em", "m" },
        { "gw", "m" },
        { "jb", "o" },
    };

    /// <summary>A NetworkManager without the flush, by other names, is left alone with a reason that names it.</summary>
    [Fact]
    public void ForgePatchLeavesManagersWithoutTheFlush()
    {
        ClassPatchResult result = OutboundPackets.Patches[index: 0].Apply(CreateManager(ForgeManager, flush: "flushOutboundQueue", checksEmptiness: true));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "func_150733_h", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Minecraft 1.14.4 polls the queue until it is empty, under the queue's monitor, so Forge's manager of that shape is left alone.</summary>
    [Fact]
    public void ForgePatchLeavesTheFlushOfMinecraft1144()
    {
        ClassPatchResult result = OutboundPackets.Patches[index: 0].Apply(CreateManager(ForgeManager, flush: "func_150733_h", checksEmptiness: false));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Forge's flush becomes synchronized, and its send and dispatch keep their flags.</summary>
    [Fact]
    public void ForgePatchSynchronizesTheFlush()
    {
        AssertSynchronized(Patched(CreateManager(ForgeManager, flush: "func_150733_h", checksEmptiness: true)), flush: "func_150733_h");
    }

    /// <summary>Only the names that the releases give their managers match, so another obfuscated class of the manager's shape is left alone.</summary>
    [Fact]
    public void PatchesOnlyMatchTheNamesOfTheManagers()
    {
        byte[] classFile = CreateManager(className: "zz", flush: "m", checksEmptiness: true);

        Assert.DoesNotContain(OutboundPackets.Patches, patch => patch.Matches(classFile, hierarchy: null));
    }

    /// <summary>Another release's class of a manager's name, without the flush of that naming, is left alone by the patches that match it, with the method they miss.</summary>
    [Fact]
    public void VanillaPatchesLeaveAnotherClassOfTheSameName()
    {
        byte[] classFile = CreateManager(className: "ej", flush: "m", checksEmptiness: true);
        ClassPatchResult[] results = [.. OutboundPackets.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile))];

        Assert.Equal(expected: 2, results.Length);
        Assert.All(results, static result => Assert.Contains(expectedSubstring: "ej has no ", result.SkippedReason, StringComparison.Ordinal));
    }

    /// <summary>Each vanilla release's manager gets its flush synchronized by the one patch of its naming, and its send and dispatch keep their flags.</summary>
    /// <param name="manager">The manager's obfuscated name.</param>
    /// <param name="flush">The flush's.</param>
    [Theory]
    [MemberData(nameof(VanillaManagers))]
    public void VanillaPatchesSynchronizeTheFlushOfEachRelease(string manager, string flush)
    {
        AssertSynchronized(Patched(CreateManager(manager, flush, checksEmptiness: true)), flush);
    }

    private static void AssertSynchronized(byte[] patched, string flush)
    {
        JavaClassFile model = JavaClassFile.Read(patched);

        Assert.Equal(Synchronized, Flags(model, flush, descriptor: "()V") & Synchronized);
        Assert.Equal(expected: 0, Flags(model, name: "send", SendDescriptor) & Synchronized);
        Assert.Equal(expected: 0, Flags(model, name: "dispatch", SendDescriptor) & Synchronized);
    }

    // A manager whose send flushes and then dispatches, and whose flush checks whether the queue is empty before it polls, as 1.7.2 to 1.14.3 do, or only polls,
    // as 1.14.4 does.
    private static byte[] CreateManager(string className, string flush, bool checksEmptiness)
    {
        JavaMethod dispatching = JavaMethod.Virtual(className, name: "dispatch", SendDescriptor);
        JavaMethod flushing = JavaMethod.Virtual(className, flush, descriptor: "()V");

        return new ClassFileWriter(className, Handler, majorVersion: 50)
            .AddMethod(
                name: "send",
                SendDescriptor,
                code => code.ALoad(slot: 0).Invoke(flushing).ALoad(slot: 0).ALoad(slot: 1).Invoke(dispatching).Return(),
                accessFlags: 0x0001
            )
            .AddMethod(name: "dispatch", SendDescriptor, static code => code.Return(), accessFlags: 0x0002)
            .AddMethod(
                flush,
                descriptor: "()V",
                code => code.AConstNull().Invoke(checksEmptiness ? IsEmpty : Poll).Pop().ALoad(slot: 0).AConstNull().Invoke(dispatching).Return(),
                accessFlags: 0x0002
            )
            .ToArray();
    }

    private static int Flags(JavaClassFile model, string name, string descriptor)
    {
        return (model.FindMethod(name, descriptor) ?? throw new InvalidOperationException($"{model.Name} has no {name}{descriptor}.")).AccessFlags;
    }

    // The class as the one patch that matches and changes it leaves it, as the agent applies the patches.
    private static byte[] Patched(byte[] classFile)
    {
        ClassPatchResult[] changes =
        [
            .. OutboundPackets.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile)).Where(static result => result.Changed),
        ];

        return Assert.Single(changes).Bytes ?? throw new InvalidOperationException(changes[0].ToString());
    }
}
