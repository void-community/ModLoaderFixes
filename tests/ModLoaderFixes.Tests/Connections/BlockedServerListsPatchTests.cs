using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Connections;

using Xunit;

namespace ModLoaderFixes.Tests.Connections;

/// <summary>Pointing patchy's read of Mojang's list of blocked servers at the helper that reads the whole list first, in patchy 2's supplier and 1.3.9's initializer.</summary>
public sealed class BlockedServerListsPatchTests
{
    // patchy 1.3.9 reads the list in the class initializer of BlockedServers; in patchy 2 the class of that name is only the predicate that the supplier builds.
    private const string BlockedServers = "com/mojang/patchy/BlockedServers";
    private const string ClassInitializer = "<clinit>";

    // The method in which patchy 2's supplier reads the list, and the predicate it returns.
    private const string CreateBlockList = "createBlockList";
    private const string CreateBlockListDescriptor = "()Ljava/util/function/Predicate;";

    // The read that the patch redirects: lines of BufferedReader, which returns a Stream. The helper's method keeps the name.
    private const string Lines = "lines";
    private const string Reader = "java/io/BufferedReader";
    private const string StreamDescriptor = "()Ljava/util/stream/Stream;";

    // patchy 2's supplier, the class that declares createBlockList.
    private const string Supplier = "com/mojang/patchy/MojangBlockListSupplier";

    /// <summary>patchy 2's BlockedServers, the predicate the supplier builds, reads no list itself, so its class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesPatchy2sPredicate()
    {
        ClassPatchResult skipped = Apply(new ClassFileWriter(BlockedServers).AddMethod(name: "test", descriptor: "(Ljava/lang/String;)Z", static code => code.IConst(value: 0).IReturn())
            .ToArray());

        Assert.False(skipped.Changed);
        Assert.Contains(ClassInitializer, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>patchy 2's createBlockList and patchy 1.3.9's initializer read the list through the helper, which takes the reader.</summary>
    /// <param name="className">The internal name of the class that reads the list: patchy 2's supplier, or patchy 1.3.9's BlockedServers.</param>
    /// <param name="method">The name of the method that reads it there: the supplier's createBlockList, or the class initializer of BlockedServers.</param>
    /// <param name="descriptor">The descriptor of that method, which returns a Predicate from createBlockList and nothing from the initializer.</param>
    /// <exception cref="InvalidOperationException">
    /// The patch for <paramref name="className"/> left the class unchanged although <paramref name="method"/> reads a reader's lines, so there are no patched bytes
    /// in which to look for the helper's call; the message is the patch's own line about the class.
    /// </exception>
    [Theory]
    [InlineData(Supplier, CreateBlockList, CreateBlockListDescriptor)]
    [InlineData(BlockedServers, ClassInitializer, "()V")]
    public void PatchReadsTheListThroughTheHelper(string className, string method, string descriptor)
    {
        ClassPatchResult applied = Apply(CreateReader(className, method, descriptor));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);

        ClassFileAssert.Calls(patched,
            method,
            owner: "modloaderfixes/BlockLists",
            Lines,
            descriptor: "(Ljava/io/BufferedReader;)Ljava/util/stream/Stream;");

        ClassFileAssert.DoesNotCall(patched, method, Reader, Lines, StreamDescriptor);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return BlockedServerLists.Patches.Single(patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
    }

    // A method that reads a null reader's lines and drops them, as patchy collects them into its list; static for the initializer, an instance method otherwise.
    private static byte[] CreateReader(string className, string method, string descriptor)
    {
        JavaMethod lines = JavaMethod.Virtual(Reader, Lines, StreamDescriptor);
        bool isInitializer = method == ClassInitializer;

        return new ClassFileWriter(className)
            .AddMethod(method,
                descriptor,
                code => isInitializer ? code.AConstNull().Invoke(lines).Pop().Return() : code.AConstNull().Invoke(lines).Pop().AConstNull().AReturn(),
                isInitializer ? 0x0008 : 0x0001)
            .ToArray();
    }
}
