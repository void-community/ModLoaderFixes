using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Keeping new connections in Minecraft 1.7's network tick: the tick's redirects in Forge and PaperSpigot, and the initializer's in vanilla.</summary>
public sealed class PendingConnectionsPatchTests
{
    private const string AddLastDescriptor = "(Ljava/lang/String;Lio/netty/channel/ChannelHandler;)Lio/netty/channel/ChannelPipeline;";
    private const string Channel = "io/netty/channel/Channel";
    private const string ChannelSetup = "initChannel";
    private const string ChannelSetupDescriptor = "(Lio/netty/channel/Channel;)V";
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Helper = "modloaderfixes/Connections";
    private const string Initializer = "io/netty/channel/ChannelInitializer";
    private const string NewConnections = "modloaderfixes/NewConnections";
    private const string PaperManager = "net/minecraft/server/v1_7_R4/NetworkManager";
    private const string Pipeline = "io/netty/channel/ChannelPipeline";
    private const string VanillaManager = "ej";

    /// <summary>A NetworkSystem without the tick, as in Minecraft with other names, is left alone with a reason.</summary>
    [Fact]
    public void ForgePatchLeavesSystemsWithoutTheTick()
    {
        ClassPatchResult result = PendingConnections.Forge.Apply(
            CreateTick(
                className: "net/minecraft/network/NetworkSystem",
                ForgeManager,
                tick: "networkTick",
                isOpen: "func_150724_d",
                process: "func_74428_b"
            )
        );

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "func_151269_c", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Forge's tick sends its check of the channel and its processing of packets both to the helper, with the manager as their argument.</summary>
    [Fact]
    public void ForgePatchRedirectsTheCheckAndTheProcessing()
    {
        AssertRedirected(
            PendingConnections.Forge,
            CreateTick(
                className: "net/minecraft/network/NetworkSystem",
                ForgeManager,
                tick: "func_151269_c",
                isOpen: "func_150724_d",
                process: "func_74428_b"
            ),
            ForgeManager,
            tick: "func_151269_c",
            isOpen: "func_150724_d",
            process: "func_74428_b"
        );
    }

    /// <summary>PaperSpigot's NetworkManager gains the getter of its private channel as an Object, since PaperSpigot relocates Netty's Channel.</summary>
    [Fact]
    public void PaperChannelPatchAddsTheGetter()
    {
        byte[] classFile = new ClassFileWriter(PaperManager).AddField(name: "m", descriptor: "Lnet/minecraft/util/io/netty/channel/Channel;").ToArray();

        ClassPatchResult result = PendingConnections.PaperChannel.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AddedMembers);
        Assert.NotNull(JavaClassFile.Read(patched).FindMethod(name: "modloaderfixes$channel", descriptor: "()Ljava/lang/Object;"));
    }

    /// <summary>PaperSpigot's tick sends its check of the channel and its processing of packets to the same helper, by CraftBukkit's names.</summary>
    [Fact]
    public void PaperPatchRedirectsTheCheckAndTheProcessing()
    {
        AssertRedirected(
            PendingConnections.Paper,
            CreateTick(className: "net/minecraft/server/v1_7_R4/ServerConnection", PaperManager, tick: "c", isOpen: "isConnected", process: "a"),
            PaperManager,
            tick: "c",
            isOpen: "isConnected",
            process: "a"
        );
    }

    /// <summary>Minecraft 1.8 constructs its connection with the packet direction, and its tick needs no fix, so its initializer is left alone.</summary>
    [Fact]
    public void VanillaPatchLeavesMinecraft18()
    {
        ClassPatchResult result = PendingConnections.Vanilla.Apply(CreateInitializer(className: "rd", connectionConstructor: "(Lie;)V"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The vanilla patch only looks at the default package, where vanilla's obfuscated classes are, so Forge's and PaperSpigot's initializers don't match.</summary>
    [Fact]
    public void VanillaPatchOnlyMatchesTheDefaultPackage()
    {
        Assert.False(
            PendingConnections.Vanilla.Matches(CreateInitializer(className: "net/minecraft/network/NetworkSystem$2", connectionConstructor: "(Z)V"), hierarchy: null)
        );
    }

    /// <summary>
    /// Vanilla 1.7's initializer, found by its shape under any name, sends its List.add and the addLast that names the connection to the helper, and keeps its other
    /// addLast calls.
    /// </summary>
    [Fact]
    public void VanillaPatchRedirectsTheAddsOfTheConnection()
    {
        Assert.True(PendingConnections.Vanilla.Matches(CreateInitializer(className: "nd", connectionConstructor: "(Z)V"), hierarchy: null));

        ClassPatchResult result = PendingConnections.Vanilla.Apply(CreateInitializer(className: "nd", connectionConstructor: "(Z)V"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        ClassFileAssert.Calls(patched, ChannelSetup, NewConnections, name: "add", descriptor: "(Ljava/util/List;Ljava/lang/Object;)Z");
        ClassFileAssert.Calls(patched, ChannelSetup, NewConnections, name: "addLast", "(Lio/netty/channel/ChannelPipeline;" + AddLastDescriptor[1..]);
        ClassFileAssert.Calls(patched, ChannelSetup, Pipeline, name: "addLast", AddLastDescriptor);
        ClassFileAssert.DoesNotCall(patched, ChannelSetup, owner: "java/util/List", name: "add", descriptor: "(Ljava/lang/Object;)Z");
    }

    private static void AssertRedirected(ClassPatch patch, byte[] classFile, string manager, string tick, string isOpen, string process)
    {
        ClassPatchResult result = patch.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 2, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, tick, Helper, name: "isOpenOrPending", "(L" + manager + ";)Z");
        ClassFileAssert.Calls(patched, tick, Helper, name: "processIfActive", "(L" + manager + ";)V");
        ClassFileAssert.DoesNotCall(patched, tick, manager, isOpen, descriptor: "()Z");
        ClassFileAssert.DoesNotCall(patched, tick, manager, process, descriptor: "()V");
    }

    // A channel initializer that adds handlers to the pipeline, then constructs a connection, adds it to a list, and adds it to the pipeline as packet_handler,
    // as vanilla's does.
    private static byte[] CreateInitializer(string className, string connectionConstructor)
    {
        JavaMethod pipeline = JavaMethod.Interface(Channel, name: "pipeline", descriptor: "()Lio/netty/channel/ChannelPipeline;");
        JavaMethod addLast = JavaMethod.Interface(Pipeline, name: "addLast", AddLastDescriptor);
        JavaMethod add = JavaMethod.Interface(owner: "java/util/List", name: "add", descriptor: "(Ljava/lang/Object;)Z");

        return new ClassFileWriter(className, Initializer, majorVersion: 50)
            .AddMethod(
                ChannelSetup,
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
                accessFlags: 0x0004
            )
            .ToArray();
    }

    // A tick class whose method checks a null manager's channel and processes its packets when it is open, as the tick does for each manager.
    private static byte[] CreateTick(string className, string manager, string tick, string isOpen, string process)
    {
        JavaMethod isChannelOpen = JavaMethod.Virtual(manager, isOpen, descriptor: "()Z");
        JavaMethod processReceivedPackets = JavaMethod.Virtual(manager, process, descriptor: "()V");

        // The branch needs a class of version 50, which the JVM verifies without stack map frames.
        return new ClassFileWriter(className, majorVersion: 50)
            .AddMethod(
                tick,
                descriptor: "()V",
                code =>
                {
                    CodeLabel done = code.NewLabel();

                    return code.AConstNull().Invoke(isChannelOpen).If(Opcodes.IfEq, done).AConstNull().Invoke(processReceivedPackets).Mark(done).Return();
                }
            )
            .ToArray();
    }
}
