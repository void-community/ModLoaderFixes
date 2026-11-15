using JvmBridge.Agents;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Makes the map of union file systems that securejarhandler's <c>UnionFileSystemProvider</c> keeps a concurrent one. The provider registers and removes a
/// file system under the map's lock, but <c>getFileSystem</c> reads the plain <c>HashMap</c> without it. A registration that grows the map publishes the new
/// table before it has moved the entries into it, so a lookup at that moment misses a file system that is registered: it throws
/// <c>FileSystemNotFoundException</c>, or finds the key and then gets null. Up to securejarhandler 3.0.9, FancyModLoader 9.0.13 and securemodules 2.2.3 every
/// class load from a jar makes that lookup, so a class that the loading screen's thread first needs while mod discovery registers the mods' jars fails to
/// load. A NeoForge 1.21.6 client's loading screen then left its buffer building, and the game crashed with <c>Already building.</c> as it drew the screen
/// itself. Every release with the class has the unlocked read: <c>cpw.mods:securejarhandler</c> 0.9.0 to 3.0.9, which Forge 1.17.1 to 1.20.2 and NeoForge up
/// to 21.6 ship, FancyModLoader's own 9.0.0 to 10.0.2 in NeoForge 21.6 to 21.8, and Forge's securemodules 2.2.2 to 2.2.24 from Forge 1.20.2. The patch makes
/// the constructor create a <c>ConcurrentHashMap</c>, whose readers need no lock; the writers keep theirs.
/// </summary>
internal static class UnionFileSystems
{
    /// <summary>Gets the patch of the provider's constructor, under the one name the class has in securejarhandler, FancyModLoader and securemodules.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "cpw/mods/niofs/union/UnionFileSystemProvider")
        .Method(name: "<init>", descriptor: "()V")
        .ReplaceConstruction(fromClass: "java/util/HashMap", toClass: "java/util/concurrent/ConcurrentHashMap", storedIn: "fileSystems");
}
