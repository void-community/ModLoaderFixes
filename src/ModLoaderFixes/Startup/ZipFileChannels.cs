using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Keeps a zip file system's jar readable when a thread reading it is interrupted. The JDK's <c>ZipFileSystem</c> reads every entry through the one channel its
/// constructor opens with <c>Files.newByteChannel</c>, a <c>FileChannel</c> that an interrupt during a read closes for every thread. Forge reads mod jars through
/// zip file systems from 1.13.2, and every jar of its module layer from 1.17, and NeoForge up to 1.21.8, so an interrupted class load leaves the loader nothing
/// in that jar, and each class it hadn't loaded fails with <c>NoClassDefFoundError</c>. The patch points the constructor's call at
/// <c>modloaderfixes.ZipChannels</c>, whose channel opens the file again when an interrupt closed it. It matches the class in Java 8's <c>com.sun.nio.zipfs</c>
/// and in <c>jdk.nio.zipfs</c> from Java 9.
/// </summary>
internal static class ZipFileChannels
{
    private static readonly JavaMethod NewByteChannel = JavaMethod.Static(
        owner: "java/nio/file/Files",
        name: "newByteChannel",
        descriptor: "(Ljava/nio/file/Path;[Ljava/nio/file/OpenOption;)Ljava/nio/channels/SeekableByteChannel;"
    );

    /// <summary>Gets the patch of <c>ZipFileSystem</c>'s constructor.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.ForMatching(
            static className => className is "jdk/nio/zipfs/ZipFileSystem" or "com/sun/nio/zipfs/ZipFileSystem",
            description: "the JDK's zip file system"
        )
        .Method(name: "<init>")
        .RedirectInvocation(NewByteChannel, toOwner: "modloaderfixes/ZipChannels");
}
