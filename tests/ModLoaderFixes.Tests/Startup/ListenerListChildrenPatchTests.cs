using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Making the children of EventBus's listener lists before 4.0.0 a synchronized list that addChild fills under the parent's lock and forceRebuild iterates a copy of.</summary>
public sealed class ListenerListChildrenPatchTests
{
    // addChild, which creates the children's list and adds a child to it, and the ArrayList that it creates the list as before the patch.
    private const string AddChild = "addChild";
    private const string AddChildDescriptor = "(L" + ClassName + ";)V";
    private const string ArrayList = "java/util/ArrayList";

    // The class of EventBus's listener list, and its method that iterates the children.
    private const string ClassName = "net/minecraftforge/eventbus/ListenerList$ListenerListInst";
    private const string ForceRebuild = "forceRebuild";

    // The Java helper that makes the children's list and iterates a copy of it, and the type of the field that holds the children.
    private const string Helper = "modloaderfixes/ListenerChildren";
    private const string ListDescriptor = "Ljava/util/List;";

    /// <summary>EventBus 4.0.0 and later create a synchronized list already, so their class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesEventBus4()
    {
        ClassPatchResult skipped = ListenerListChildren.Patch.Apply(CreateList(isSynchronized: true));

        Assert.False(skipped.Changed);
        Assert.Contains(expectedSubstring: "synchronizedList", skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>addChild gets its list from the helper and locks the parent, and forceRebuild iterates the helper's copy.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left the list that keeps its children in a plain ArrayList unchanged, so there is no patched class to look into; the message is the patch's
    /// own account of what it did.
    /// </exception>
    [Fact]
    public void PatchLocksTheChildren()
    {
        ClassPatchResult applied = ListenerListChildren.Patch.Apply(CreateList(isSynchronized: false));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);

        // The patch replaced the list's construction, redirected the iteration and made addChild synchronized.
        Assert.Equal(expected: 1, applied.ReplacedConstructions);
        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        Assert.Equal(expected: 0x0022, model.FindMethod(AddChild, AddChildDescriptor)?.AccessFlags);

        // addChild takes its list from the helper and no longer makes an ArrayList, and forceRebuild iterates through the helper.
        ClassFileAssert.Calls(patched, AddChild, Helper, name: "newChildren", descriptor: "()Ljava/util/List;");
        ClassFileAssert.DoesNotCall(patched, AddChild, ArrayList, name: "<init>", descriptor: "()V");

        ClassFileAssert.Calls(patched,
            ForceRebuild,
            Helper,
            name: "iterator",
            descriptor: "(Ljava/util/List;)Ljava/util/Iterator;");
    }

    // A ListenerListInst whose addChild stores a new ArrayList in children, wrapped in Collections.synchronizedList from EventBus 4.0.0, and adds the child to it,
    // and whose forceRebuild asks the children for their iterator, as EventBus's do without their null checks.
    private static byte[] CreateList(bool isSynchronized)
    {
        JavaMethod synchronizedList = JavaMethod.Static(owner: "java/util/Collections",
            name: "synchronizedList",
            descriptor: "(Ljava/util/List;)Ljava/util/List;");

        JavaMethod add = JavaMethod.Interface(owner: "java/util/List", name: "add", descriptor: "(Ljava/lang/Object;)Z");

        JavaMethod iterator = JavaMethod.Interface(owner: "java/util/List",
            name: "iterator",
            descriptor: "()Ljava/util/Iterator;");

        CodeBuilder Add(CodeBuilder code)
        {
            CodeBuilder created = code.ALoad(slot: 0)
                .New(ArrayList)
                .Dup()
                .InvokeSpecial(ArrayList, name: "<init>", descriptor: "()V");

            return (isSynchronized ? created.Invoke(synchronizedList) : created).PutField(ClassName, name: "children", ListDescriptor)
                .ALoad(slot: 0)
                .GetField(ClassName, name: "children", ListDescriptor)
                .ALoad(slot: 1)
                .Invoke(add)
                .Pop()
                .Return();
        }

        return new ClassFileWriter(ClassName).AddField(name: "children", ListDescriptor)
            .AddMethod(AddChild, AddChildDescriptor, Add, accessFlags: 0x0002)
            .AddMethod(ForceRebuild,
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .GetField(ClassName, name: "children", ListDescriptor)
                    .Invoke(iterator)
                    .Pop()
                    .Return(),
                accessFlags: 0x0004)
            .ToArray();
    }
}
