using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Connections;

/// <summary>
/// Makes patchy treat a list of blocked servers that it can't read to its end as one it can't fetch. Minecraft clients from 1.12 ask patchy for the list the first
/// time they connect to a server, in a static initializer: patchy 1.3.9's <c>BlockedServers</c>, which its replacement of Netty's <c>Bootstrap</c> checks each
/// address with up to 1.16.5, and from 1.17 <c>ServerNameResolver</c>'s, through patchy 2's <c>MojangBlockListSupplier.createBlockList</c>. patchy reads the list
/// with <c>BufferedReader.lines()</c> and catches <c>IOException</c>, but <c>lines()</c> reports a read that fails part way as an <c>UncheckedIOException</c>,
/// which escapes the initializer as an <c>ExceptionInInitializerError</c>. The connecting screen's thread catches only exceptions, so it dies and the screen
/// stays, and every later connection fails as the class can't be initialized any more. The patches point the call of <c>lines()</c> at
/// <c>modloaderfixes.BlockLists</c>, which reads the whole list first and throws the <c>IOException</c> patchy catches.
/// </summary>
internal static class BlockedServerLists
{
    private const string Helper = "modloaderfixes/BlockLists";

    private static readonly JavaMethod Lines = JavaMethod.Virtual(owner: "java/io/BufferedReader", name: "lines", descriptor: "()Ljava/util/stream/Stream;");

    /// <summary>Gets the patches: of patchy 2's <c>createBlockList</c>, in 2.1.6 and 2.2.10, and of patchy 1.3.9's <c>BlockedServers</c> initializer.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        ClassPatch.For(className: "com/mojang/patchy/MojangBlockListSupplier")
            .Method(name: "createBlockList", descriptor: "()Ljava/util/function/Predicate;")
            .RedirectInvocation(Lines, Helper),
        ClassPatch.For(className: "com/mojang/patchy/BlockedServers").Method(name: "<clinit>", descriptor: "()V").RedirectInvocation(Lines, Helper),
    ];
}
