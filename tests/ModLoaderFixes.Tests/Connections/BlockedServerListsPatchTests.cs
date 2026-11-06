using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Connections;

using Xunit;

namespace ModLoaderFixes.Tests.Connections;

/// <summary>Pointing patchy's read of Mojang's list of blocked servers at the helper that reads the whole list first, in patchy 2's supplier and 1.3.9's initializer.</summary>
public sealed class BlockedServerListsPatchTests
{
    private const string BlockedServers = "com/mojang/patchy/BlockedServers";
    private const string ClassInitializer = "<clinit>";
    private const string CreateBlockList = "createBlockList";
    private const string CreateBlockListDescriptor = "()Ljava/util/function/Predicate;";
    private const string Lines = "lines";
    private const string Reader = "java/io/BufferedReader";
    private const string StreamDescriptor = "()Ljava/util/stream/Stream;";
    private const string Supplier = "com/mojang/patchy/MojangBlockListSupplier";

    /// <summary>patchy 2's BlockedServers, the predicate the supplier builds, reads no list itself, so its class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesPatchy2sPredicate()
    {
        ClassPatchResult result = Apply(
            new ClassFileWriter(BlockedServers).AddMethod(name: "test", descriptor: "(Ljava/lang/String;)Z", static code => code.IConst(value: 0).IReturn()).ToArray()
        );

        Assert.False(result.Changed);
        Assert.Contains(ClassInitializer, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>patchy 2's createBlockList and patchy 1.3.9's initializer read the list through the helper, which takes the reader.</summary>
    [Theory]
    [InlineData(Supplier, CreateBlockList, CreateBlockListDescriptor)]
    [InlineData(BlockedServers, ClassInitializer, "()V")]
    public void PatchReadsTheListThroughTheHelper(string className, string method, string descriptor)
    {
        ClassPatchResult result = Apply(CreateReader(className, method, descriptor));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, method, owner: "modloaderfixes/BlockLists", Lines, descriptor: "(Ljava/io/BufferedReader;)Ljava/util/stream/Stream;");
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
            .AddMethod(
                method,
                descriptor,
                code => isInitializer ? code.AConstNull().Invoke(lines).Pop().Return() : code.AConstNull().Invoke(lines).Pop().AConstNull().AReturn(),
                isInitializer ? 0x0008 : 0x0001
            )
            .ToArray();
    }
}
