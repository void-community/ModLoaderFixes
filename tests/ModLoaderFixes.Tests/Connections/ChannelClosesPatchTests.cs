using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using ModLoaderFixes.Connections;

using Xunit;

namespace ModLoaderFixes.Tests.Connections;

/// <summary>
/// Pointing the close of a Minecraft 1.7.2 to 1.8 connection's channel at the helper that sends what is unsent first: Forge's by FML's names, vanilla's under each
/// release's names, and PaperSpigot 1.7.10's, whose Netty CraftBukkit relocates.
/// </summary>
public sealed class ChannelClosesPatchTests
{
    private const string Channel = "io/netty/channel/Channel";
    private const string CloseDescriptor = "()Lio/netty/channel/ChannelFuture;";
    private const string ForgeCloseChannel = "func_150718_a";
    private const string ForgeDescriptor = "(Lnet/minecraft/util/IChatComponent;)V";
    private const string ForgeManager = "net/minecraft/network/NetworkManager";
    private const string Helper = "modloaderfixes/ChannelCloses";
    private const string HelperDescriptor = "(Lio/netty/channel/Channel;)Lio/netty/channel/ChannelFuture;";
    private const string PaperChannel = "net/minecraft/util/io/netty/channel/Channel";
    private const string PaperManager = "net/minecraft/server/v1_7_R4/NetworkManager";

    /// <summary>Forge's closeChannel sends its close of the channel to the helper, with the channel as its argument, and keeps its check that the channel is open.</summary>
    [Fact]
    public void ForgePatchRedirectsTheClose()
    {
        byte[] classFile = CreateManager(ForgeManager, ForgeCloseChannel, ForgeDescriptor, Channel);

        ClassPatchResult result = Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, ForgeCloseChannel, Helper, name: "close", HelperDescriptor);
        ClassFileAssert.Calls(patched, ForgeCloseChannel, Channel, name: "isOpen", descriptor: "()Z");
        ClassFileAssert.DoesNotCall(patched, ForgeCloseChannel, Channel, name: "close", CloseDescriptor);
    }

    /// <summary>
    /// PaperSpigot's close sends its close of the channel to PaperSpigot's own helper, whose argument is the channel of the package that CraftBukkit relocates Netty
    /// to.
    /// </summary>
    [Fact]
    public void PaperPatchRedirectsTheCloseToItsOwnHelper()
    {
        const string paperDescriptor = "(Lnet/minecraft/server/v1_7_R4/IChatBaseComponent;)V";

        byte[] classFile = CreateManager(PaperManager, closeChannel: "close", paperDescriptor, PaperChannel);

        ClassPatchResult result = ChannelCloses.Paper.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(
            patched,
            method: "close",
            owner: "modloaderfixes/PaperChannelCloses",
            name: "close",
            "(L" + PaperChannel + ";)Lnet/minecraft/util/io/netty/channel/ChannelFuture;"
        );
        ClassFileAssert.DoesNotCall(patched, method: "close", PaperChannel, name: "close", descriptor: "()Lnet/minecraft/util/io/netty/channel/ChannelFuture;");
    }

    /// <summary>A NetworkManager without closeChannel under FML's names, as in Minecraft with other names, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesAManagerWithoutCloseChannel()
    {
        ClassPatchResult result = Apply(CreateManager(ForgeManager, closeChannel: "closeChannel", ForgeDescriptor, Channel));

        Assert.False(result.Changed);
        Assert.Contains(ForgeCloseChannel, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Minecraft 1.8.1 gives its NetworkManager the name that 1.8 gives its own, but another name to the chat component that closeChannel takes, so the patch finds no
    /// such method there and changes nothing in the real server: 1.8.1 sets TCP_NODELAY, and its sockets hold nothing back.
    /// </summary>
    [Fact]
    public async Task PatchLeavesTheRealServerOfMinecraft181()
    {
        string jar = await RealJarTests.VanillaServer(
            version: "1.8.1",
            sha1: "68bfb524888f7c0ab939025e07e5de08843dac0f",
            sha256: "ef5f5a1a1a78087859b18153acf97efc6ecb12540ac08d82b9c95024249b9845"
        ).GetPathAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ChannelCloses.Patches.SelectMany(patch => patch.ApplyToJar(jar)), static result => result.Changed);
    }

    /// <summary>
    /// In the real NetworkManager of each vanilla naming from 1.7.2 to 1.8, closeChannel hands its channel to the helper and no longer closes it itself, and no
    /// other class of the server changes.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves its server under.</param>
    /// <param name="sha256">The server's pinned checksum.</param>
    /// <param name="className">The NetworkManager's name in that server.</param>
    /// <param name="descriptor">closeChannel's descriptor there, with the name that the release gives the chat component.</param>
    [Theory]
    [InlineData(
        "1.7.2",
        "3716cac82982e7c2eb09f83028b555e9ea606002",
        "b4139899700c1bdbf72880eec4bdb9e46c2cf22d1724a48a018ee0330035462e",
        "ef",
        "(Lfa;)V"
    )]
    [InlineData(
        "1.7.4",
        "61220311cef80aecc4cd8afecd5f18ca6b9461ff",
        "796d6ca283861a3185f2e87ec321b1233540cdf2638da6e00f1d96d47791031f",
        "ef",
        "(Lfa;)V"
    )]
    [InlineData(
        "1.7.5",
        "e1d557b2e31ea881404e41b05ec15c810415e060",
        "caa9e13aee32112b3b5305c02ecc01c05502fe244cdb83faa7a01832937542e4",
        "eg",
        "(Lfg;)V"
    )]
    [InlineData(
        "1.7.6",
        "41ea7757d4d7f74b95fc1ac20f919a8e521e910c",
        "5cdc6ab6168ae496ce1d1ae96c0b165360184518d030417429ec7687c0b9d527",
        "ej",
        "(Lfj;)V"
    )]
    [InlineData(
        "1.7.10",
        "952438ac4e01b4d115c5fc38f891710c4941df29",
        "c70870f00c4024d829e154f7e5f4e885b02dd87991726a3308d81f513972f3fc",
        "ej",
        "(Lfj;)V"
    )]
    [InlineData(
        "1.8",
        "a028f00e678ee5c6aef0e29656dca091b5df11c7",
        "40e23f3823d6f0e3cbadc491cedb55b8ba53f8ab516b68182ddd1536babeb291",
        "gr",
        "(Lho;)V"
    )]
    public async Task PatchRedirectsTheCloseOfARealServer(string version, string sha1, string sha256, string className, string descriptor)
    {
        string jar = await RealJarTests.VanillaServer(version, sha1, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        ClassPatchResult result = Assert.Single(ChannelCloses.Patches.SelectMany(patch => patch.ApplyToJar(jar)), static result => result.Changed);
        JavaClassFile model = JavaClassFile.Read(result.Bytes ?? throw new InvalidOperationException(result.ToString()));
        IReadOnlyList<Instruction> closeChannel = model.FindMethod(name: "a", descriptor)?.Code?.Instructions ?? [];
        Instruction call = Assert.Single(closeChannel, static instruction => instruction.Opcode == Opcodes.InvokeStatic);
        MemberReference called = model.GetMemberReference(call.ConstantIndex ?? 0);

        Assert.Equal(className, result.ClassName);
        Assert.Equal((Helper, "close", HelperDescriptor), (called.Owner, called.Name, called.Descriptor));
        Assert.DoesNotContain(closeChannel, instruction => Invokes(model, instruction, name: "close"));
        Assert.Contains(closeChannel, instruction => Invokes(model, instruction, name: "isOpen"));
    }

    /// <summary>
    /// Another release can give one of its classes the name of a naming's NetworkManager, as 1.8.1 does with 1.8's; a class that lacks the naming's closeChannel is
    /// left alone.
    /// </summary>
    [Fact]
    public void VanillaPatchLeavesAClassOfAnotherReleaseWithTheSameName()
    {
        byte[] classFile = CreateManager(className: "gr", closeChannel: "a", descriptor: "(Lhp;)V", Channel);

        Assert.All(
            ChannelCloses.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)),
            patch => Assert.False(patch.Apply(classFile).Changed)
        );
    }

    /// <summary>Vanilla's closeChannel sends its close of the channel to the same helper under the names of each release from 1.7.2 to 1.8.</summary>
    /// <param name="className">The NetworkManager's obfuscated name.</param>
    /// <param name="descriptor">closeChannel's descriptor, with the obfuscated name of the chat component.</param>
    [Theory]
    [InlineData("ef", "(Lfa;)V")]
    [InlineData("eg", "(Lfg;)V")]
    [InlineData("ej", "(Lfj;)V")]
    [InlineData("gr", "(Lho;)V")]
    public void VanillaPatchRedirectsTheCloseUnderEachNaming(string className, string descriptor)
    {
        byte[] classFile = CreateManager(className, closeChannel: "a", descriptor, Channel);

        ClassPatchResult result = Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, method: "a", Helper, name: "close", HelperDescriptor);
        ClassFileAssert.DoesNotCall(patched, method: "a", Channel, name: "close", CloseDescriptor);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return ChannelCloses.Patches.Single(patch => patch.Matches(classFile, hierarchy: null) && patch != ChannelCloses.Paper).Apply(classFile);
    }

    // A connection whose closeChannel closes its channel when it is open, as Minecraft's does, with the channel in a field of the class.
    private static byte[] CreateManager(string className, string closeChannel, string descriptor, string channel)
    {
        string channelDescriptor = "L" + channel + ";";
        JavaMethod isOpen = JavaMethod.Interface(channel, name: "isOpen", descriptor: "()Z");
        JavaMethod close = JavaMethod.Interface(channel, name: "close", "()L" + channel + "Future;");

        // The branch needs a class of version 50, which the JVM verifies without stack map frames, and which Minecraft 1.7 and 1.8 are compiled to.
        return new ClassFileWriter(className, majorVersion: 50)
            .AddField(name: "channel", channelDescriptor)
            .AddMethod(
                closeChannel,
                descriptor,
                code =>
                {
                    CodeLabel closed = code.NewLabel();

                    return code.ALoad(slot: 0)
                        .GetField(className, name: "channel", channelDescriptor)
                        .Invoke(isOpen)
                        .If(Opcodes.IfEq, closed)
                        .ALoad(slot: 0)
                        .GetField(className, name: "channel", channelDescriptor)
                        .Invoke(close)
                        .Pop()
                        .Mark(closed)
                        .Return();
                },
                accessFlags: 0x0001
            )
            .ToArray();
    }

    private static bool Invokes(JavaClassFile model, Instruction instruction, string name)
    {
        bool isInterfaceCall = instruction.Opcode == Opcodes.InvokeInterface;

        return isInterfaceCall && instruction.ConstantIndex is { } index && model.GetMemberReference(index) is { Owner: Channel } member && member.Name == name;
    }
}
