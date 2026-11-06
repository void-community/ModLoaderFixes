using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes Forge's handshake handle an indexed reply before it marks the reply answered. <c>indexFirst</c> builds the handler of the client's mod list and of its
/// acknowledgements as <c>biConsumerFor(handleIndexedMessage).andThen(biConsumerFor(next))</c>, and each half looks the connection's handler up in the channel's
/// <c>FML_HANDSHAKE_HANDLER</c> attribute on its own. The first half removes the reply's index from <c>sentMessages</c>, and once the last one is gone the server
/// thread's <c>tickServer</c> sets the attribute to null, so the second half, on the network thread, can find no handler and drop the player with a
/// <c>NullPointerException</c>. With the halves the other way round, the reply's index stays in <c>sentMessages</c> while its own handler runs, and nothing looks the
/// handler up once the index is gone. Forge for Minecraft 1.13.2 to 1.20.1 and NeoForge for 1.20.1 have the race, and NeoForge for 1.20.2 to 1.20.4-20.4.69-beta,
/// whose <c>indexFirst</c> combines the halves with <c>consumerFor</c> and <c>MessageFunctions.MessageConsumer.andThen</c>.
/// </summary>
internal static class HandshakeReplies
{
    private const string Description = "indexed replies handled before they are marked answered";
    private const string MethodName = "indexFirst";

    /// <summary>
    /// Gets the patch, which matches each class that has declared <c>indexFirst</c>: <c>NetworkInitialization</c> in 1.13.2, the handler in <c>fml.network</c> in 1.14 to
    /// 1.16, in <c>fmllegacy.network</c> in 1.17 and in <c>network</c> from 1.18, and NeoForge's from 1.20.2.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.ForMatching(
            static className => className
                is "net/minecraftforge/fml/network/NetworkInitialization"
                or "net/minecraftforge/fml/network/FMLHandshakeHandler"
                or "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler"
                or "net/minecraftforge/network/HandshakeHandler"
                or "net/neoforged/neoforge/network/HandshakeHandler",
            description: "Forge's and NeoForge's builders of handshake reply handlers"
        )
        .Edit(Description, static context => Reorder(context.Editor));

    // indexFirst keeps the first half in local 1 and ends with aload_1, aload_0, invokestatic of the class's combiner and invokeinterface andThen, which become aload_0,
    // invokestatic of the combiner, aload_1 and invokeinterface andThen at the same length. Forge's combiner is biConsumerFor, whose result is a BiConsumer, and
    // NeoForge's is consumerFor, whose result is a MessageConsumer; andThen is the one of that result's own type. Code of another shape, as a class already patched
    // has, is left alone, and so is a class without indexFirst, as NetworkInitialization from 1.14 is.
    private static int Reorder(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;
        IReadOnlyList<Instruction>? code = model.FindMethod(MethodName)?.Code?.Instructions;
        int call = code?.ToList().FindIndex(static instruction => instruction.Opcode == Opcodes.InvokeInterface) ?? -1;

        if (code is null || call < 3 || code[call] is not { ConstantIndex: { } chainIndex })
            return 0;

        if (code[call - 1] is not { Opcode: Opcodes.InvokeStatic, ConstantIndex: { } index })
            return 0;

        MemberReference chaining = model.GetMemberReference(chainIndex);
        MemberReference combining = model.GetMemberReference(index);

        if (chaining.Name != "andThen" || chaining.Descriptor != "(L" + chaining.Owner + ";)L" + chaining.Owner + ";")
            return 0;

        if (combining.Name is not ("biConsumerFor" or "consumerFor") || combining.Owner != model.Name)
            return 0;

        JavaMethod andThen = JavaMethod.Interface(chaining.Owner, chaining.Name, chaining.Descriptor);
        JavaMethod combine = JavaMethod.Static(model.Name, combining.Name, combining.Descriptor);
        byte[] original = editor.AssembleCode(builder => builder.ALoad(slot: 1).ALoad(slot: 0).Invoke(combine).Invoke(andThen));
        byte[] replacement = editor.AssembleCode(builder => builder.ALoad(slot: 0).Invoke(combine).ALoad(slot: 1).Invoke(andThen));

        return editor.ReplaceCode(MethodName, methodDescriptor: null, code[call - 3].Offset, original, replacement) ? 1 : 0;
    }
}
