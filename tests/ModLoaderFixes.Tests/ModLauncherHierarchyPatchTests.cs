using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Getting the maps of ModLauncher's class hierarchy cache from the helper that makes them safe to share, in TransformerClassWriter's class initializer.</summary>
public sealed class ModLauncherHierarchyPatchTests
{
    // ModLauncher's class writer and its class initializer, which makes the maps of the hierarchy cache.
    private const string ClassInitializer = "<clinit>";
    private const string ClassName = "cpw/mods/modlauncher/TransformerClassWriter";

    // What the patch swaps: the HashMap that the initializer constructs, and the helper whose factories, all of one descriptor, return the map to store in its place.
    private const string FactoryDescriptor = "()Ljava/util/HashMap;";
    private const string HashMap = "java/util/HashMap";
    private const string Helper = "modloaderfixes/ClassHierarchyCache";

    /// <summary>Each of the three maps ModLauncher 2 to 5 create comes from the helper's factory for it, and the class creates no HashMap of its own any more.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left a class writer whose initializer stores HashMaps in the fields of ModLauncher 2 to 5 unchanged, so it has no bytes to check; the message is
    /// the patch's own line on why.
    /// </exception>
    [Fact]
    public void PatchGetsTheThreeMapsFromTheHelper()
    {
        ClassPatchResult applied = ModLauncherHierarchies.Patch
            .Apply(CreateClassFile(HashMap, fieldDescriptor: "Ljava/util/HashMap;", fields: ["classParents", "classHierarchies", "isInterface"]));

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        // Each of the three constructions became a call of the helper's factory for the map of that field.
        Assert.Equal(expected: 3, applied.ReplacedConstructions);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "parents", FactoryDescriptor);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "hierarchies", FactoryDescriptor);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "interfaces", FactoryDescriptor);

        // The initializer constructs no HashMap itself any more.
        ClassFileAssert.DoesNotCall(patched, ClassInitializer, HashMap, name: "<init>", descriptor: "()V");
    }

    /// <summary>ModLauncher 8 keeps ConcurrentHashMaps in fields of other names, so its class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesModLauncher8Alone()
    {
        ClassPatchResult skipped = ModLauncherHierarchies.Patch
            .Apply(CreateClassFile(className: "java/util/concurrent/ConcurrentHashMap",
                fieldDescriptor: "Ljava/util/Map;",
                fields: ["CLASS_PARENTS", "CLASS_HIERARCHIES", "IS_INTERFACE"]));

        Assert.False(skipped.Changed);
        Assert.Contains("no construction of " + HashMap, skipped.SkippedReason, StringComparison.Ordinal);
    }

    // A static initializer that stores a new map of the given class in each of the given fields, as TransformerClassWriter's does.
    private static byte[] CreateClassFile(string className, string fieldDescriptor, params string[] fields)
    {
        CodeBuilder StoreNew(CodeBuilder code, string field)
        {
            return code.New(className)
                .Dup()
                .InvokeSpecial(className, name: "<init>", descriptor: "()V")
                .PutStatic(ClassName, field, fieldDescriptor);
        }

        return new ClassFileWriter(ClassName).AddMethod(ClassInitializer, descriptor: "()V", code => fields.Aggregate(code, StoreNew).Return(), accessFlags: 0x0008)
            .ToArray();
    }
}
