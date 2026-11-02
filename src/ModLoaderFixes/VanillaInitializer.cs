using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Points the two calls of vanilla Minecraft 1.7's channel initializers that add a new connection at <c>modloaderfixes.NewConnections</c>. The initializers are
/// <c>NetworkSystem</c>'s anonymous classes under each release's names, and another release gives other classes those names, so the edit checks what
/// <c>initChannel</c> does: the initializer constructs the connection with a boolean, which Minecraft 1.8 replaced with the packet direction, adds it to the
/// tick's list with <c>List.add</c>, and puts it into the pipeline as <c>packet_handler</c>. Its other calls are left alone.
/// </summary>
internal static class VanillaInitializer
{
    /// <summary>The descriptor of the initializer's method.</summary>
    internal const string MethodDescriptor = "(Lio/netty/channel/Channel;)V";

    /// <summary>The name of the initializer's method, which Netty declares, so obfuscation leaves it.</summary>
    internal const string MethodName = "initChannel";

    private const string AddLastDescriptor = "(Ljava/lang/String;Lio/netty/channel/ChannelHandler;)Lio/netty/channel/ChannelPipeline;";
    private const string ConnectionName = "packet_handler";
    private const string Helper = "modloaderfixes/NewConnections";

    private static readonly HashSet<string> Names =
    [
        .. MappedClasses.NetworkInitializer.Vanilla.Select(static naming => naming.Name),
        .. MappedClasses.MemoryInitializer.Vanilla.Select(static naming => naming.Name),
    ];

    private static readonly JavaMethod AddLast = JavaMethod.Interface(owner: "io/netty/channel/ChannelPipeline", name: "addLast", AddLastDescriptor);
    private static readonly JavaMethod ListAdd = JavaMethod.Interface(owner: "java/util/List", name: "add", descriptor: "(Ljava/lang/Object;)Z");

    /// <summary>
    /// Redirects the initializer's one <c>List.add</c>, and the <c>addLast</c> that puts the connection in the pipeline under its name, when the method has the shape
    /// of Minecraft 1.7's. A method that lacks any part of it isn't that initializer, and is left alone.
    /// </summary>
    /// <param name="context">The class and the scoped method.</param>
    /// <returns>The calls redirected, two, or none for a class that isn't the initializer.</returns>
    internal static int Defer(ClassEditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClassFileEditor editor = context.Editor;
        JavaClassFile model = editor.Model;
        IReadOnlyList<Instruction>? code = model.FindMethod(MethodName, MethodDescriptor)?.Code?.Instructions;

        if (code is null || !ConstructsConnection(model, code))
            return 0;

        Instruction[] additions = [.. code.Where(instruction => model.Invokes(instruction, ListAdd))];
        int naming = IndexOfName(model, code);

        if (additions.Length != 1 || naming < 0)
            return 0;

        // The name is the first argument of the addLast that puts the connection in the pipeline, so that call is the next addLast after it.
        Instruction? addLast = code.Skip(naming + 1).FirstOrDefault(instruction => model.Invokes(instruction, AddLast));

        return addLast is not { } connectionAddLast
            ? 0
            : Redirect(editor, additions[0], ListAdd, name: "add") + Redirect(editor, connectionAddLast, AddLast, name: "addLast");
    }

    /// <summary>Gets whether a class has the name of one of the initializers in some vanilla release.</summary>
    /// <param name="name">The class's internal name.</param>
    /// <returns>Whether it may be an initializer.</returns>
    internal static bool IsInitializer(string name)
    {
        return Names.Contains(name);
    }

    // Minecraft 1.7 constructs its connection with a boolean that says whether it is a client's; 1.8 passes the packet direction instead, and its tick needs no fix.
    private static bool ConstructsConnection(JavaClassFile model, IReadOnlyList<Instruction> code)
    {
        return code.Any(
            instruction =>
            instruction.Opcode == Opcodes.InvokeSpecial
            && instruction.ConstantIndex is { } index
            && model.GetMemberReference(index) is { Name: "<init>", Descriptor: "(Z)V" }
        );
    }

    private static int IndexOfName(JavaClassFile model, IReadOnlyList<Instruction> code)
    {
        for (int index = 0; index < code.Count; index++)
        {
            if (LoadsName(model, code[index]))
                return index;
        }

        return -1;
    }

    private static bool LoadsName(JavaClassFile model, Instruction instruction)
    {
        bool loadsConstant = instruction.Opcode is Opcodes.Ldc or Opcodes.LdcW;

        return loadsConstant && instruction.ConstantIndex is { } constant && model.ConstantPool[constant] is StringEntry text && model.GetUtf8(text.StringIndex) == ConnectionName;
    }

    // invokeinterface takes five bytes and invokestatic three, so the call becomes invokestatic of the helper, with the receiver as its first argument, and two nops.
    private static int Redirect(ClassFileEditor editor, Instruction site, JavaMethod from, string name)
    {
        JavaMethod to = JavaMethod.Static(Helper, name, "(L" + from.Owner + ";" + from.Descriptor[1..]);
        byte[] original = editor.AssembleCode(code => code.Invoke(from));
        byte[] replacement = editor.AssembleCode(code => code.Invoke(to).Nop().Nop());

        return !editor.ReplaceCode(MethodName, MethodDescriptor, site.Offset, original, replacement)
            ? throw new InvalidOperationException($"{MethodName} doesn't call {from} at offset {site.Offset}.")
            : 1;
    }
}
