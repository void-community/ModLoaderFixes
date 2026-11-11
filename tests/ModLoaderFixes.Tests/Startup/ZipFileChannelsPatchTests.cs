using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Pointing the JDK's zip file system's channel at the helper that opens it again after an interrupt, in Java 8's package and in Java 9's module.</summary>
public sealed class ZipFileChannelsPatchTests
{
    private const string NewByteChannel = "newByteChannel";
    private const string NewByteChannelDescriptor = "(Ljava/nio/file/Path;[Ljava/nio/file/OpenOption;)Ljava/nio/channels/SeekableByteChannel;";

    /// <summary>Only the JDK's zip file systems match.</summary>
    [Fact]
    public void PatchOnlyMatchesTheZipFileSystems()
    {
        Assert.False(ZipFileChannels.Patch.Matches(CreateFileSystem(className: "cpw/mods/niofs/union/UnionFileSystem"), hierarchy: null));
    }

    /// <summary>The constructor opens its channel through the helper.</summary>
    [Theory]
    [InlineData("jdk/nio/zipfs/ZipFileSystem")]
    [InlineData("com/sun/nio/zipfs/ZipFileSystem")]
    public void PatchOpensTheChannelThroughTheHelper(string className)
    {
        ClassPatchResult result = ZipFileChannels.Patch.Apply(CreateFileSystem(className));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, method: "<init>", owner: "modloaderfixes/ZipChannels", NewByteChannel, NewByteChannelDescriptor);
        ClassFileAssert.DoesNotCall(patched, method: "<init>", owner: "java/nio/file/Files", NewByteChannel, NewByteChannelDescriptor);
    }

    // A constructor that opens its channel with Files.newByteChannel from a null path and no options, and stores nothing, as ZipFileSystem's opens its own.
    private static byte[] CreateFileSystem(string className)
    {
        JavaMethod open = JavaMethod.Static(owner: "java/nio/file/Files", NewByteChannel, NewByteChannelDescriptor);

        return new ClassFileWriter(className)
            .AddMethod(
                name: "<init>",
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .InvokeSpecial(owner: "java/lang/Object", name: "<init>", descriptor: "()V")
                    .AConstNull()
                    .IConst(value: 0)
                    .ANewArray(className: "java/nio/file/OpenOption")
                    .Invoke(open)
                    .Pop()
                    .Return(),
                accessFlags: 0x0001
            )
            .ToArray();
    }
}
