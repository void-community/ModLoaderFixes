using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;

using Xunit;

namespace ModLoaderFixes.Tests.Handshakes;

/// <summary>Swapping the halves of Forge's indexed reply handlers in <c>indexFirst</c>, so that a reply's own handler runs before the reply is marked answered.</summary>
public sealed class HandshakeRepliesPatchTests
{
    private const string Combine = "biConsumerFor";
    private const string IndexFirst = "indexFirst";

    private static readonly JavaMethod AndThen = JavaMethod.Interface(
        owner: "java/util/function/BiConsumer",
        name: "andThen",
        descriptor: "(Ljava/util/function/BiConsumer;)Ljava/util/function/BiConsumer;"
    );

    /// <summary>Gets each class that has declared <c>indexFirst</c>, with the type of the consumer that its <c>biConsumerFor</c> takes.</summary>
    public static TheoryData<string, string> Builders { get; } = new()
    {
        { "net/minecraftforge/fml/network/NetworkInitialization", "net/minecraftforge/fml/util/ThreeConsumer" },
        { "net/minecraftforge/fml/network/FMLHandshakeHandler", "net/minecraftforge/fml/util/ThreeConsumer" },
        { "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler", "net/minecraftforge/fmllegacy/util/ThreeConsumer" },
        { "net/minecraftforge/network/HandshakeHandler", "net/minecraftforge/network/HandshakeHandler$HandshakeConsumer" },
    };

    /// <summary>Once patched, <c>indexFirst</c> composes the handler of the reply first and the removal of its index after it, from the same two halves.</summary>
    /// <param name="className">The class that declares <c>indexFirst</c>.</param>
    /// <param name="consumer">The type of the consumer that <c>biConsumerFor</c> takes.</param>
    [Theory]
    [MemberData(nameof(Builders))]
    public void PatchHandlesTheReplyBeforeItsIndex(string className, string consumer)
    {
        ClassPatchResult result = HandshakeReplies.Patch.Apply(CreateBuilder(className, consumer, reordered: false));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(Opcodes(CreateBuilder(className, consumer, reordered: true)), Opcodes(patched));
    }

    /// <summary>An <c>indexFirst</c> that already composes the reply's handler first has nothing to swap.</summary>
    [Fact]
    public void PatchLeavesBuildersThatHandleTheReplyFirst()
    {
        ClassPatchResult result = HandshakeReplies.Patch.Apply(
            CreateBuilder(
                className: "net/minecraftforge/network/HandshakeHandler",
                consumer: "net/minecraftforge/network/HandshakeHandler$HandshakeConsumer",
                reordered: true
            )
        );

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary><c>NetworkInitialization</c> from 1.14 calls the handler's <c>indexFirst</c> instead of declaring one, so it is left alone.</summary>
    [Fact]
    public void PatchLeavesClassesWithoutIndexFirst()
    {
        byte[] classFile = new ClassFileWriter(name: "net/minecraftforge/fml/network/NetworkInitialization")
            .AddMethod(name: "getHandshakeChannel", descriptor: "()V", static code => code.Return(), accessFlags: 0x0008)
            .ToArray();

        Assert.False(HandshakeReplies.Patch.Apply(classFile).Changed);
    }

    /// <summary>Only the classes that have declared <c>indexFirst</c> match.</summary>
    [Fact]
    public void PatchOnlyMatchesTheBuilders()
    {
        byte[] classFile = CreateBuilder(className: "net/minecraftforge/network/simple/SimpleChannel", consumer: "java/lang/Object", reordered: false);

        Assert.False(HandshakeReplies.Patch.Matches(classFile, hierarchy: null));
    }

    // indexFirst as Forge compiles it, with aconst_null in place of the invokedynamic of handleIndexedMessage's method reference, which ClassFileWriter doesn't write.
    private static byte[] CreateBuilder(string className, string consumer, bool reordered)
    {
        JavaMethod combine = JavaMethod.Static(className, Combine, "(L" + consumer + ";)Ljava/util/function/BiConsumer;");

        CodeBuilder Compose(CodeBuilder code)
        {
            CodeBuilder first = code.AConstNull().Invoke(combine).AStore(slot: 1);

            return reordered
                ? first.ALoad(slot: 0).Invoke(combine).ALoad(slot: 1).Invoke(AndThen).AReturn()
                : first.ALoad(slot: 1).ALoad(slot: 0).Invoke(combine).Invoke(AndThen).AReturn();
        }

        return new ClassFileWriter(className)
            .AddMethod(IndexFirst, "(L" + consumer + ";)Ljava/util/function/BiConsumer;", Compose, accessFlags: 0x0009)
            .ToArray();
    }

    private static List<byte> Opcodes(byte[] classFile)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);
        CodeAttribute code = model.FindMethod(IndexFirst)?.Code ?? throw new InvalidOperationException(message: "The class has no indexFirst.");

        return [.. code.Instructions.Select(static instruction => instruction.Opcode)];
    }
}
