using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Handing Minecraft's <c>ChannelAccess.clear</c> to the sound executor, so that no channel's stream is closed while the sound thread decodes it.</summary>
public sealed class SoundChannelsPatchTests
{
    private const string Channels = "Ljava/util/Set;";
    private const string Executor = "Ljava/util/concurrent/Executor;";
    private const string MovedBody = "modloaderfixes$clear";

    private static readonly JavaMethod ForEach = JavaMethod.Interface(owner: "java/util/Set", name: "forEach", descriptor: "(Ljava/util/function/Consumer;)V");
    private static readonly JavaMethod Clear = JavaMethod.Interface(owner: "java/util/Set", name: "clear", descriptor: "()V");

    /// <summary>Gets <c>ChannelAccess</c> under Forge's names up to 1.16.5 and from 1.17, and under the names of vanilla 1.16.3, with the name of its <c>clear</c>.</summary>
    public static TheoryData<string, string> Classes { get; } = new()
    {
        { "net/minecraft/client/audio/ChannelManager", "func_217936_b" },
        { "net/minecraft/client/sounds/ChannelAccess", "m_120302_" },
        { "enf", "b" },
    };

    /// <summary>A class that the patch has moved <c>clear</c> of already is left as it is.</summary>
    [Fact]
    public void PatchLeavesAPatchedClassAlone()
    {
        ClassPatchResult first = Apply(CreateChannelAccess(className: "enf", clear: "b", majorVersion: 52));
        ClassPatchResult second = Apply(first.Bytes ?? throw new InvalidOperationException(first.ToString()));

        Assert.False(second.Changed);
    }

    /// <summary>Minecraft 1.20.5 and later compile for Java 21 and decode with JOrbis, so their <c>ChannelAccess</c> is left alone.</summary>
    [Fact]
    public void PatchLeavesClassesCompiledForJava21Alone()
    {
        Assert.False(Apply(CreateChannelAccess(className: "net/minecraft/client/sounds/ChannelAccess", clear: "clear", majorVersion: 65)).Changed);
    }

    /// <summary>
    /// Another release can give one of its other classes a vanilla release's name, and such a class, without the set it releases with <c>forEach</c> and the
    /// executor, is left alone.
    /// </summary>
    [Fact]
    public void PatchLeavesOtherClassesOfTheSameNameAlone()
    {
        byte[] classFile = new ClassFileWriter(name: "enf", superName: "java/lang/Object", accessFlags: 0x0021, majorVersion: 52)
            .AddField(name: "a", Executor, accessFlags: 0x0012)
            .AddMethod(name: "b", descriptor: "()V", static code => code.Return(), accessFlags: 0x0001)
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    /// <summary>Once patched, <c>clear</c> runs its old code, moved to a method of its own, on the executor and waits for it.</summary>
    /// <param name="className">The class's name.</param>
    /// <param name="clear">The name of its <c>clear</c>.</param>
    [Theory]
    [MemberData(nameof(Classes))]
    public void PatchReleasesTheChannelsOnTheExecutor(string className, string clear)
    {
        ClassPatchResult result = Apply(CreateChannelAccess(className, clear, majorVersion: 52));
        JavaClassFile patched = JavaClassFile.Read(result.Bytes ?? throw new InvalidOperationException(result.ToString()));

        Assert.Equal(
            [
                Opcodes.ALoad0,
                Opcodes.InvokeDynamic,
                Opcodes.ALoad0,
                Opcodes.GetField,
                Opcodes.InvokeStatic,
                Opcodes.InvokeVirtual,
                Opcodes.Pop,
                Opcodes.Return,
            ],
            OpcodesOf(patched, clear)
        );
        Assert.Equal(OpcodesOf(JavaClassFile.Read(CreateChannelAccess(className, clear, majorVersion: 52)), clear), OpcodesOf(patched, MovedBody));
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string name = JavaClassFile.Read(classFile).Name;

        return SoundChannels.Patches.Where(patch => patch.Matches(classFile, hierarchy: null)).Select(patch => patch.Apply(classFile)).Single(result => result.ClassName == name);
    }

    // ChannelAccess as Minecraft compiles it, with aconst_null in place of the invokedynamic of ChannelHandle::release, and with the private method of
    // scheduleTick's lambda, which takes no parameters either but walks the set with an iterator.
    private static byte[] CreateChannelAccess(string className, string clear, int majorVersion)
    {
        return new ClassFileWriter(className, superName: "java/lang/Object", accessFlags: 0x0021, majorVersion)
            .AddField(name: "a", Channels, accessFlags: 0x0012)
            .AddField(name: "c", Executor, accessFlags: 0x0012)
            .AddMethod(
                clear,
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .GetField(className, name: "a", Channels)
                    .AConstNull()
                    .Invoke(ForEach)
                    .ALoad(slot: 0)
                    .GetField(className, name: "a", Channels)
                    .Invoke(Clear)
                    .Return(),
                accessFlags: 0x0001
            )
            .AddMethod(
                name: "c",
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .GetField(className, name: "a", Channels)
                    .Invoke(JavaMethod.Interface(owner: "java/util/Set", name: "iterator", descriptor: "()Ljava/util/Iterator;"))
                    .Pop()
                    .Return(),
                accessFlags: 0x1002
            )
            .ToArray();
    }

    private static List<byte> OpcodesOf(JavaClassFile model, string method)
    {
        CodeAttribute code = model.FindMethod(method, descriptor: "()V")?.Code ?? throw new InvalidOperationException(model.Name + " has no " + method + "()V.");

        return [.. code.Instructions.Select(static instruction => instruction.Opcode)];
    }
}
