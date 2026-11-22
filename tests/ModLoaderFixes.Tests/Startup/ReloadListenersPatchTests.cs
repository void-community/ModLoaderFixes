using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Locking the set of the listeners that a Minecraft reload waits for, in the reload's constructor, by Forge's, vanilla's and CraftBukkit's names.</summary>
public sealed class ReloadListenersPatchTests
{
    // The reload's constructor, which takes the listeners and creates the set of those it still waits for.
    private const string Constructor = "<init>";
    private const string ConstructorDescriptor = "(Ljava/util/List;)V";

    // The call that creates the set: newHashSet, of the Java helper once the class is patched and of Guava's Sets before.
    private const string Helper = "modloaderfixes/ReloadListeners";
    private const string NewHashSet = "newHashSet";
    private const string NewHashSetDescriptor = "(Ljava/lang/Iterable;)Ljava/util/HashSet;";
    private const string Sets = "com/google/common/collect/Sets";

    /// <summary>The reload's constructor gets its set from the helper under each name it runs under, and constructs its barriers as before.</summary>
    /// <param name="className">
    /// The reload's internal name, one that a patch matches: Forge's by MCP's name or by Mojang's, a vanilla release's obfuscated one, or CraftBukkit's
    /// <c>Reloadable</c> in one of its packages. The barrier that the constructor creates is named after it.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No patch that matches <paramref name="className"/> changed the reload, so there is no patched class to look into; the message is the account of the last
    /// patch that matched.
    /// </exception>
    [Theory]
    [InlineData("net/minecraft/resources/AsyncReloader")]
    [InlineData("net/minecraft/server/packs/resources/SimpleReloadInstance")]
    [InlineData("acl")]
    [InlineData("agf")]
    [InlineData("net/minecraft/server/v1_16_R1/Reloadable")]
    [InlineData("net/minecraft/server/v1_16_R3/Reloadable")]
    [InlineData("net/minecraft/server/packs/resources/Reloadable")]
    public void PatchCreatesTheListenersThroughTheHelper(string className)
    {
        ClassPatchResult applied = Apply(CreateReload(className, className + "$1"));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        ClassFileAssert.Calls(patched, Constructor, Helper, NewHashSet, NewHashSetDescriptor);
        ClassFileAssert.DoesNotCall(patched, Constructor, Sets, NewHashSet, NewHashSetDescriptor);
        ClassFileAssert.Calls(patched, Constructor, className + "$1", Constructor, "(L" + className + ";)V");
    }

    /// <summary>A reload under Forge's name that creates its set otherwise isn't the code this fix was written for, so it is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesAReloadWithoutGuavasSet()
    {
        const string className = "net/minecraft/server/packs/resources/SimpleReloadInstance";
        ClassPatchResult skipped = Apply(CreateReload(className, className + "$1", setOwner: "com/example/Sets"));

        Assert.False(skipped.Changed);
        Assert.Contains(NewHashSet, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Another release's class under a reload's obfuscated name doesn't construct the reload's barrier, so it is left alone.</summary>
    [Fact]
    public void PatchLeavesAnotherClassUnderAReloadsName()
    {
        ClassPatchResult skipped = Apply(CreateReload(className: "acl", barrier: "acl$2"));

        Assert.False(skipped.Changed);
    }

    // Applies the patches that match the class, as the agent does, and gives the result of the one that changed it, or of the last one otherwise.
    private static ClassPatchResult Apply(byte[] classFile)
    {
        ClassPatch[] matching = [.. ReloadListeners.Patches.Where(patch => patch.Matches(classFile, hierarchy: null))];
        ClassPatchResult[] results = [.. matching.Select(patch => patch.Apply(classFile))];

        Assert.NotEmpty(results);

        return results.FirstOrDefault(static result => result.Changed) ?? results[^1];
    }

    // A reload constructor of Minecraft 1.16's shape: it stores Sets.newHashSet(listeners) in its set and constructs a barrier, the reload's first anonymous class.
    private static byte[] CreateReload(string className, string barrier, string setOwner = Sets)
    {
        return new ClassFileWriter(className).AddField(name: "d", descriptor: "Ljava/util/Set;")
            .AddMethod(Constructor,
                ConstructorDescriptor,
                code => code.ALoad(slot: 0)
                    .InvokeSpecial(owner: "java/lang/Object", Constructor, descriptor: "()V")
                    .ALoad(slot: 0)
                    .ALoad(slot: 1)
                    .Invoke(JavaMethod.Static(setOwner, NewHashSet, NewHashSetDescriptor))
                    .PutField(className, name: "d", descriptor: "Ljava/util/Set;")
                    .New(barrier)
                    .Dup()
                    .ALoad(slot: 0)
                    .InvokeSpecial(barrier, Constructor, "(L" + className + ";)V")
                    .Pop()
                    .Return())
            .ToArray();
    }
}
