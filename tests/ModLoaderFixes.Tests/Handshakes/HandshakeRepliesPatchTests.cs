using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests.Handshakes;

/// <summary>
/// Swapping the halves of Forge's and NeoForge's indexed reply handlers in <c>indexFirst</c>, so that a reply's own handler runs before the reply is marked answered.
/// </summary>
public sealed class HandshakeRepliesPatchTests
{
    private const string BiConsumer = "java/util/function/BiConsumer";
    private const string Combine = "biConsumerFor";
    private const string IndexFirst = "indexFirst";

    /// <summary>
    /// Gets each class that has declared <c>indexFirst</c>, with the type of the consumer that its combiner takes, the combiner's name and the type it returns:
    /// Forge's <c>biConsumerFor</c> a BiConsumer, NeoForge's <c>consumerFor</c> from 1.20.2 a MessageConsumer.
    /// </summary>
    public static TheoryData<string, string, string, string> Builders { get; } = new()
    {
        { "net/minecraftforge/fml/network/NetworkInitialization", "net/minecraftforge/fml/util/ThreeConsumer", Combine, BiConsumer },
        { "net/minecraftforge/fml/network/FMLHandshakeHandler", "net/minecraftforge/fml/util/ThreeConsumer", Combine, BiConsumer },
        { "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler", "net/minecraftforge/fmllegacy/util/ThreeConsumer", Combine, BiConsumer },
        { "net/minecraftforge/network/HandshakeHandler", "net/minecraftforge/network/HandshakeHandler$HandshakeConsumer", Combine, BiConsumer },
        {
            "net/neoforged/neoforge/network/HandshakeHandler", "net/neoforged/neoforge/network/HandshakeHandler$HandshakeConsumer", "consumerFor",
            "net/neoforged/neoforge/network/simple/MessageFunctions$MessageConsumer"
        },
    };

    /// <summary>Once patched, <c>indexFirst</c> composes the handler of the reply first and the removal of its index after it, from the same two halves.</summary>
    /// <param name="className">The class that declares <c>indexFirst</c>.</param>
    /// <param name="consumer">The type of the consumer that the combiner takes.</param>
    /// <param name="combiner">The combiner's name.</param>
    /// <param name="result">The type the combiner returns, whose <c>andThen</c> composes the halves.</param>
    [Theory]
    [MemberData(nameof(Builders))]
    public void PatchHandlesTheReplyBeforeItsIndex(string className, string consumer, string combiner, string result)
    {
        ClassPatchResult patchResult = HandshakeReplies.Patch.Apply(CreateBuilder(className, consumer, reordered: false, combiner, result));
        byte[] patched = patchResult.Bytes ?? throw new InvalidOperationException(patchResult.ToString());

        Assert.Equal(Opcodes(CreateBuilder(className, consumer, reordered: true, combiner, result)), Opcodes(patched));
        ClassFileAssert.Calls(patched, IndexFirst, result, name: "andThen", "(L" + result + ";)L" + result + ";");
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

    // indexFirst as Forge and NeoForge compile it, with aconst_null in place of the invokedynamic of handleIndexedMessage's method reference, which ClassFileWriter
    // doesn't write.
    private static byte[] CreateBuilder(string className, string consumer, bool reordered, string combiner = Combine, string result = BiConsumer)
    {
        JavaMethod combine = JavaMethod.Static(className, combiner, "(L" + consumer + ";)L" + result + ";");
        JavaMethod andThen = JavaMethod.Interface(result, name: "andThen", "(L" + result + ";)L" + result + ";");

        CodeBuilder Compose(CodeBuilder code)
        {
            CodeBuilder first = code.AConstNull().Invoke(combine).AStore(slot: 1);

            return reordered
                ? first.ALoad(slot: 0).Invoke(combine).ALoad(slot: 1).Invoke(andThen).AReturn()
                : first.ALoad(slot: 1).ALoad(slot: 0).Invoke(combine).Invoke(andThen).AReturn();
        }

        return new ClassFileWriter(className)
            .AddMethod(IndexFirst, "(L" + consumer + ";)L" + result + ";", Compose, accessFlags: 0x0009)
            .ToArray();
    }

    private static List<byte> Opcodes(byte[] classFile)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);
        CodeAttribute code = model.FindMethod(IndexFirst)?.Code ?? throw new InvalidOperationException(message: "The class has no indexFirst.");

        return [.. code.Instructions.Select(static instruction => instruction.Opcode)];
    }
}
