using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests.Handshakes;

/// <summary>Making the list of Forge's handshake messages that wait for a reply a CopyOnWriteArrayList, in the handler's constructor.</summary>
public sealed class SentMessagesPatchTests
{
    private const string ArrayList = "java/util/ArrayList";
    private const string Constructor = "<init>";
    private const string CopyOnWriteArrayList = "java/util/concurrent/CopyOnWriteArrayList";
    private const string ListDescriptor = "Ljava/util/List;";

    /// <summary>Gets the names that Forge has given its handshake handler: one in 1.13 to 1.16, another in 1.17, and a third from 1.18.</summary>
    /// <value>One row per name, whose only column is the internal name of the handler's class.</value>
    public static TheoryData<string> HandlerNames { get; } =
    [
        "net/minecraftforge/fml/network/FMLHandshakeHandler",
        "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler",
        "net/minecraftforge/network/HandshakeHandler",
    ];

    /// <summary>
    /// NeoForge's handler from 1.20.2 constructs its <c>sentMessages</c> as the helper's IntArrayList, whose methods lock, and the other IntArrayList the constructor
    /// creates stays one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// NeoForge's patch left the handler unchanged although its constructor stores a new IntArrayList in <c>sentMessages</c>, and the message is the patch's own line
    /// about the class; or the patched handler has no constructor with code to read the created classes from.
    /// </exception>
    [Fact]
    public void NeoForgePatchCreatesSentMessagesAsTheLockedList()
    {
        const string intArrayList = "it/unimi/dsi/fastutil/ints/IntArrayList";
        const string className = "net/neoforged/neoforge/network/HandshakeHandler";

        static CodeBuilder StoreNewList(CodeBuilder code, string field)
        {
            return code.ALoad(slot: 0)
                .New(intArrayList)
                .Dup()
                .InvokeSpecial(intArrayList, Constructor, descriptor: "()V")
                .PutField(className, field, "L" + intArrayList + ";");
        }

        byte[] classFile = new ClassFileWriter(className)
            .AddMethod(Constructor,
                descriptor: "()V",
                static code => StoreNewList(StoreNewList(code, field: "sentMessages"), field: "otherIndexes").Return(),
                accessFlags: 0x0001)
            .ToArray();

        ClassPatchResult applied = HandshakeSentMessages.NeoForgePatch.Apply(classFile);
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.ReplacedConstructions);
        Assert.Equal(expected: ["modloaderfixes/HandshakeIndexes", intArrayList], CreatedClasses(patched));
    }

    /// <summary>The list stored in <c>sentMessages</c> becomes a CopyOnWriteArrayList, and the other list the constructor creates stays an ArrayList.</summary>
    /// <param name="className">The handler's name.</param>
    /// <exception cref="InvalidOperationException">
    /// The patch left the handler named <paramref name="className"/> unchanged although its constructor stores a new ArrayList in <c>sentMessages</c>, and the
    /// message is the patch's own line about the class; or the patched handler has no constructor with code to read the created classes from.
    /// </exception>
    [Theory]
    [MemberData(nameof(HandlerNames))]
    public void PatchCreatesSentMessagesAsACopyOnWriteArrayList(string className)
    {
        ClassPatchResult applied = Apply(CreateClassFile(className, firstField: "sentMessages"));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.ReplacedConstructions);
        Assert.Equal(expected: [CopyOnWriteArrayList, ArrayList], CreatedClasses(patched));
        ClassFileAssert.Calls(patched, Constructor, CopyOnWriteArrayList, Constructor, descriptor: "()V");
        ClassFileAssert.Calls(patched, Constructor, ArrayList, Constructor, descriptor: "()V");
    }

    /// <summary>A constructor that creates its lists for other fields isn't the handler this fix was written for, so the class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConstructorsThatDontCreateSentMessages()
    {
        ClassPatchResult skipped = Apply(CreateClassFile(className: "net/minecraftforge/network/HandshakeHandler",
            firstField: "pendingMessages"));

        Assert.False(skipped.Changed);
        Assert.Contains("no construction of " + ArrayList, skipped.SkippedReason, StringComparison.Ordinal);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return HandshakeSentMessages.Patch.Apply(classFile);
    }

    // A constructor that stores a new ArrayList in the given field and then another one in packetBacklog, as Forge's handler from 1.18 does.
    private static byte[] CreateClassFile(string className, string firstField)
    {
        CodeBuilder StoreNewList(CodeBuilder code, string field)
        {
            return code.ALoad(slot: 0)
                .New(ArrayList)
                .Dup()
                .InvokeSpecial(ArrayList, Constructor, descriptor: "()V")
                .PutField(className, field, ListDescriptor);
        }

        return new ClassFileWriter(className)
            .AddMethod(Constructor,
                descriptor: "()V",
                code => StoreNewList(StoreNewList(code, firstField), field: "packetBacklog").Return(),
                accessFlags: 0x0001)
            .ToArray();
    }

    private static List<string> CreatedClasses(byte[] classFile)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);

        CodeAttribute code = model.FindMethod(Constructor)?.Code
            ?? throw new InvalidOperationException(message: "The class has no constructor.");

        return [.. code.Instructions
            .Where(static instruction => instruction.Opcode == Opcodes.New)
            .Select(instruction => model.GetClassName(instruction.ConstantIndex ?? 0))];
    }
}
