using System.Globalization;

using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes Forge's handshake handle an indexed reply before it marks the reply answered. <c>indexFirst</c> builds the handler of the client's mod list and of its
/// acknowledgements as <c>biConsumerFor(handleIndexedMessage).andThen(biConsumerFor(next))</c>, and each half looks the connection's handler up in the channel's
/// <c>FML_HANDSHAKE_HANDLER</c> attribute on its own. The first half removes the reply's index from <c>sentMessages</c>, and once the last one is gone the server
/// thread's <c>tickServer</c> sets the attribute to null, so the second half, on the network thread, can find no handler and drop the player with a
/// <c>NullPointerException</c>. With the halves the other way round, the reply's index stays in <c>sentMessages</c> while its own handler runs, and nothing looks the
/// handler up once the index is gone. Forge for Minecraft 1.13.2 to 1.20.1 and NeoForge for 1.20.1 have the race.
/// </summary>
internal static class HandshakeReplies
{
    private const string Combine = "biConsumerFor";
    private const string Description = "indexed replies handled before they are marked answered";
    private const string MethodName = "indexFirst";

    private static readonly JavaMethod AndThen = JavaMethod.Interface(
        owner: "java/util/function/BiConsumer",
        name: "andThen",
        descriptor: "(Ljava/util/function/BiConsumer;)Ljava/util/function/BiConsumer;"
    );

    /// <summary>
    /// Gets the patch, which matches each class that has declared <c>indexFirst</c>: <c>NetworkInitialization</c> in 1.13.2, the handler in <c>fml.network</c> in 1.14 to
    /// 1.16, in <c>fmllegacy.network</c> in 1.17 and in <c>network</c> from 1.18.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.ForMatching(
            static className => className
                is "net/minecraftforge/fml/network/NetworkInitialization"
                or "net/minecraftforge/fml/network/FMLHandshakeHandler"
                or "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler"
                or "net/minecraftforge/network/HandshakeHandler",
            description: "Forge's builders of handshake reply handlers"
        )
        .Edit(Description, static context => Reorder(context.Editor));

    private static bool EndsWithComposition(JavaClassFile model, IReadOnlyList<Instruction> code, int call)
    {
        bool loadsBoth = code[call - 3].Opcode == Opcodes.ALoad1 && code[call - 2].Opcode == Opcodes.ALoad0;

        return loadsBoth
            && code[call - 1] is { Opcode: Opcodes.InvokeStatic, ConstantIndex: { } index }
            && model.GetMemberReference(index) is { Name: Combine } combining
            && combining.Owner == model.Name;
    }

    private static byte[] Operand(Instruction instruction)
    {
        int index = instruction.ConstantIndex ?? throw new InvalidOperationException("The instruction at offset " + instruction.Offset.ToString(CultureInfo.InvariantCulture) + " names no constant.");

        return [(byte)(index >> 8), (byte)index];
    }

    // indexFirst keeps the first half in local 1 and ends with aload_1, aload_0, invokestatic biConsumerFor and invokeinterface andThen, which become aload_0,
    // invokestatic biConsumerFor, aload_1 and invokeinterface andThen at the same length, with the constants the calls already name: javac 8 can give a class two
    // entries for the same class, so an assembled call may name another constant than the one in the code. Code of another shape, as a class already patched has, is
    // left alone, and so is a class without indexFirst, as NetworkInitialization from 1.14 is.
    private static int Reorder(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;
        IReadOnlyList<Instruction>? code = model.FindMethod(MethodName)?.Code?.Instructions;
        int call = code?.ToList().FindIndex(instruction => model.Invokes(instruction, AndThen)) ?? -1;

        if (code is null || call < 3 || !EndsWithComposition(model, code, call))
            return 0;

        byte[] combine = [Opcodes.InvokeStatic, .. Operand(code[call - 1])];
        byte[] andThen = [Opcodes.InvokeInterface, .. Operand(code[call]), 2, 0];
        byte[] original = [Opcodes.ALoad1, Opcodes.ALoad0, .. combine, .. andThen];
        byte[] replacement = [Opcodes.ALoad0, .. combine, Opcodes.ALoad1, .. andThen];

        return editor.ReplaceCode(MethodName, methodDescriptor: null, code[call - 3].Offset, original, replacement) ? 1 : 0;
    }
}
