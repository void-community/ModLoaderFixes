using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Keeping new connections in Minecraft 1.7's network tick: the tick's redirects in Forge and PaperSpigot, and the initializer's in vanilla.</summary>
public sealed class PendingConnectionsPatchTests
{
    // What a channel initializer works with in Netty: the channel, the method that sets it up, and the descriptor of the pipeline's addLast, which adds a handler
    // under a name.
    private const string AddLastDescriptor = "(Ljava/lang/String;Lio/netty/channel/ChannelHandler;)Lio/netty/channel/ChannelPipeline;";
    private const string Channel = "io/netty/channel/Channel";
    private const string ChannelSetup = "initChannel";
    private const string ChannelSetupDescriptor = "(Lio/netty/channel/Channel;)V";

    // The connection's class under Forge's names, and the helper that a patched tick sends its check of the channel and its processing of packets to.
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Helper = "modloaderfixes/Connections";

    // Netty's class that vanilla's initializer extends, and the helper through which the patched initializer adds its connection to the list and to the pipeline.
    private const string Initializer = "io/netty/channel/ChannelInitializer";
    private const string NewConnections = "modloaderfixes/NewConnections";

    // The connection's class under CraftBukkit's names, Netty's pipeline, which the initializer adds its handlers to, and the connection's class under the names
    // of vanilla 1.7.10.
    private const string PaperManager = "net/minecraft/server/v1_7_R4/NetworkManager";
    private const string Pipeline = "io/netty/channel/ChannelPipeline";
    private const string VanillaManager = "ej";

    /// <summary>A NetworkSystem without the tick, as in Minecraft with other names, is left alone with a reason.</summary>
    [Fact]
    public void ForgePatchLeavesSystemsWithoutTheTick()
    {
        ClassPatchResult skipped = PendingConnections.Forge
            .Apply(CreateTick(className: "net/minecraft/network/NetworkSystem",
                ForgeManager,
                tick: "networkTick",
                isOpen: "func_150724_d",
                process: "func_74428_b"));

        Assert.False(skipped.Changed);
        Assert.Contains(expectedSubstring: "func_151269_c", skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Forge's tick sends its check of the channel and its processing of packets both to the helper, with the manager as their argument.</summary>
    [Fact]
    public void ForgePatchRedirectsTheCheckAndTheProcessing()
    {
        AssertRedirected(PendingConnections.Forge,
            CreateTick(className: "net/minecraft/network/NetworkSystem",
                ForgeManager,
                tick: "func_151269_c",
                isOpen: "func_150724_d",
                process: "func_74428_b"),
            ForgeManager,
            tick: "func_151269_c",
            isOpen: "func_150724_d",
            process: "func_74428_b");
    }

    /// <summary>PaperSpigot's NetworkManager gains the getter of its private channel as an Object, since PaperSpigot relocates Netty's Channel.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a PaperSpigot NetworkManager that has the channel's field <c>m</c> unchanged, so it has no bytes to look the getter up in; the message is the
    /// patch's own line on why.
    /// </exception>
    [Fact]
    public void PaperChannelPatchAddsTheGetter()
    {
        byte[] classFile = new ClassFileWriter(PaperManager).AddField(name: "m", descriptor: "Lnet/minecraft/util/io/netty/channel/Channel;")
            .ToArray();

        ClassPatchResult applied = PendingConnections.PaperChannel.Apply(classFile);
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.AddedMembers);

        Assert.NotNull(JavaClassFile.Read(patched)
            .FindMethod(name: "modloaderfixes$channel", descriptor: "()Ljava/lang/Object;"));
    }

    /// <summary>PaperSpigot's tick sends its check of the channel and its processing of packets to the same helper, by CraftBukkit's names.</summary>
    [Fact]
    public void PaperPatchRedirectsTheCheckAndTheProcessing()
    {
        AssertRedirected(PendingConnections.Paper,
            CreateTick(className: "net/minecraft/server/v1_7_R4/ServerConnection",
                PaperManager,
                tick: "c",
                isOpen: "isConnected",
                process: "a"),
            PaperManager,
            tick: "c",
            isOpen: "isConnected",
            process: "a");
    }

    /// <summary>
    /// Minecraft 1.8 constructs its connection with the packet direction, and its tick needs no fix, so an initializer of its shape is left alone, even under a name
    /// that a 1.7 release gives its own initializer.
    /// </summary>
    [Fact]
    public void VanillaPatchLeavesMinecraft18()
    {
        ClassPatchResult skipped = PendingConnections.Vanilla
            .Apply(CreateInitializer(className: "nd", connectionConstructor: "(Lie;)V"));

        Assert.False(skipped.Changed);
        Assert.Contains(expectedSubstring: "nothing to change", skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The vanilla patch only looks at the names that the 1.7 releases give their initializers, so Forge's initializers and other obfuscated classes don't match it.
    /// </summary>
    [Fact]
    public void VanillaPatchOnlyMatchesTheNamesOfTheInitializers()
    {
        Assert.False(PendingConnections.Vanilla
            .Matches(CreateInitializer(className: "net/minecraft/network/NetworkSystem$2", connectionConstructor: "(Z)V"), hierarchy: null));

        Assert.False(PendingConnections.Vanilla
            .Matches(CreateInitializer(className: "rd", connectionConstructor: "(Z)V"), hierarchy: null));
    }

    /// <summary>
    /// Vanilla 1.7's initializer, under the name that 1.7.10 gives it, sends its List.add and the addLast that names the connection to the helper, and keeps its other
    /// addLast calls.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left an initializer of vanilla 1.7's shape, which it matches by the name <c>nd</c>, unchanged, so it has no bytes to check; the message is the
    /// patch's own line on why.
    /// </exception>
    [Fact]
    public void VanillaPatchRedirectsTheAddsOfTheConnection()
    {
        Assert.True(PendingConnections.Vanilla
            .Matches(CreateInitializer(className: "nd", connectionConstructor: "(Z)V"), hierarchy: null));

        ClassPatchResult applied = PendingConnections.Vanilla
            .Apply(CreateInitializer(className: "nd", connectionConstructor: "(Z)V"));

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        ClassFileAssert.Calls(patched,
            ChannelSetup,
            NewConnections,
            name: "add",
            descriptor: "(Ljava/util/List;Ljava/lang/Object;)Z");

        ClassFileAssert.Calls(patched,
            ChannelSetup,
            NewConnections,
            name: "addLast",
            "(Lio/netty/channel/ChannelPipeline;" + AddLastDescriptor[1..]);

        ClassFileAssert.Calls(patched, ChannelSetup, Pipeline, name: "addLast", AddLastDescriptor);

        ClassFileAssert.DoesNotCall(patched,
            ChannelSetup,
            owner: "java/util/List",
            name: "add",
            descriptor: "(Ljava/lang/Object;)Z");
    }

    private static void AssertRedirected(ClassPatch patch, byte[] classFile, string manager, string tick, string isOpen, string process)
    {
        ClassPatchResult applied = patch.Apply(classFile);
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        // Both of the tick's calls on the manager went to the helper, which takes the manager as its argument.
        Assert.Equal(expected: 2, applied.RedirectedInvocations);
        ClassFileAssert.Calls(patched, tick, Helper, name: "isOpenOrPending", "(L" + manager + ";)Z");
        ClassFileAssert.Calls(patched, tick, Helper, name: "processIfActive", "(L" + manager + ";)V");

        // The tick no longer asks the manager itself whether its channel is open, nor has it process its packets.
        ClassFileAssert.DoesNotCall(patched, tick, manager, isOpen, descriptor: "()Z");
        ClassFileAssert.DoesNotCall(patched, tick, manager, process, descriptor: "()V");
    }

    // A channel initializer that adds handlers to the pipeline, then constructs a connection, adds it to a list, and adds it to the pipeline as packet_handler,
    // as vanilla's does.
    private static byte[] CreateInitializer(string className, string connectionConstructor)
    {
        JavaMethod pipeline = JavaMethod.Interface(Channel,
            name: "pipeline",
            descriptor: "()Lio/netty/channel/ChannelPipeline;");

        JavaMethod addLast = JavaMethod.Interface(Pipeline, name: "addLast", AddLastDescriptor);
        JavaMethod add = JavaMethod.Interface(owner: "java/util/List", name: "add", descriptor: "(Ljava/lang/Object;)Z");

        return new ClassFileWriter(className, Initializer, majorVersion: 50)
            .AddMethod(ChannelSetup,
                ChannelSetupDescriptor,
                code => code.ALoad(slot: 1)
                    .Invoke(pipeline)
                    .Ldc(value: "timeout")
                    .AConstNull()
                    .Invoke(addLast)
                    .Pop()
                    .New(VanillaManager)
                    .Dup()
                    .AConstNull()
                    .InvokeSpecial(VanillaManager, name: "<init>", connectionConstructor)
                    .AStore(slot: 2)
                    .AConstNull()
                    .ALoad(slot: 2)
                    .Invoke(add)
                    .Pop()
                    .ALoad(slot: 1)
                    .Invoke(pipeline)
                    .Ldc(value: "packet_handler")
                    .ALoad(slot: 2)
                    .Invoke(addLast)
                    .Pop()
                    .Return(),
                accessFlags: 0x0004)
            .ToArray();
    }

    // A tick class whose method checks a null manager's channel and processes its packets when it is open, as the tick does for each manager.
    private static byte[] CreateTick(string className, string manager, string tick, string isOpen, string process)
    {
        JavaMethod isChannelOpen = JavaMethod.Virtual(manager, isOpen, descriptor: "()Z");
        JavaMethod processReceivedPackets = JavaMethod.Virtual(manager, process, descriptor: "()V");

        // The branch needs a class of version 50, which the JVM verifies without stack map frames.
        return new ClassFileWriter(className, majorVersion: 50)
            .AddMethod(tick,
                descriptor: "()V",
                code =>
                {
                    CodeLabel done = code.NewLabel();

                    return code.AConstNull()
                        .Invoke(isChannelOpen)
                        .If(Opcodes.IfEq, done)
                        .AConstNull()
                        .Invoke(processReceivedPackets)
                        .Mark(done)
                        .Return();
                })
            .ToArray();
    }
}
