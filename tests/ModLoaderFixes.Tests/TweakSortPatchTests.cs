using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Redirecting FML's <c>Collections.sort</c> call in CoreModManager class files.</summary>
public sealed class TweakSortPatchTests
{
    private const string ClassName = "cpw/mods/fml/relauncher/CoreModManager";
    private const string SortDescriptor = "(Ljava/util/List;Ljava/util/Comparator;)V";
    private const string SortTweakList = "sortTweakList";

    /// <summary>A CoreModManager whose <c>sortTweakList</c> doesn't sort with <c>Collections.sort</c> needs no patch.</summary>
    [Fact]
    public void PatchLeavesClassesThatDontCallCollectionsSort()
    {
        ClassPatchResult skipped = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: false)));

        Assert.False(skipped.Changed);
        Assert.Null(skipped.Bytes);
        Assert.NotNull(skipped.SkippedReason);
    }

    /// <summary>A CoreModManager without <c>sortTweakList</c>, as in Forge versions that sort differently, is left alone.</summary>
    [Fact]
    public void PatchLeavesClassesWithoutSortTweakList()
    {
        ClassPatchResult skipped = PatcherAgent.TweakSort.Apply(CreateClassFile((Name: "other", CallsSort: true)));

        Assert.False(skipped.Changed);
        Assert.Contains(SortTweakList, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The same call in any other method is somebody else's sort, so it stays as it was.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left the class unchanged although its <c>sortTweakList</c> sorts with <c>Collections.sort</c>, so there is no patched class in which the other
    /// method's call could have stayed.
    /// </exception>
    [Fact]
    public void PatchLeavesTheSortInOtherMethodsAlone()
    {
        byte[] patched = PatcherAgent.TweakSort
            .Apply(CreateClassFile((SortTweakList, CallsSort: true), (Name: "other", CallsSort: true)))
            .Bytes
            ?? throw new InvalidOperationException(message: "The class file was left unchanged.");

        ClassFileAssert.Calls(patched, method: "other", owner: "java/util/Collections", name: "sort", SortDescriptor);
    }

    /// <summary>The call in <c>sortTweakList</c> now goes to the helper, and the result says it was the only one redirected.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left the CoreModManager whose <c>sortTweakList</c> sorts with <c>Collections.sort</c> unchanged, so there is no patched class to look into;
    /// the message is the patch's own account of what it did.
    /// </exception>
    [Fact]
    public void PatchRedirectsTheSortInSortTweakList()
    {
        ClassPatchResult applied = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: true)));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        ClassFileAssert.Calls(patched, SortTweakList, JavaClasses.Modloaderfixes.Collections.Name, name: "sort", SortDescriptor);
        ClassFileAssert.DoesNotCall(patched, SortTweakList, owner: "java/util/Collections", name: "sort", SortDescriptor);
    }

    /// <summary>Bytes that aren't a class file are skipped with a reason and never throw into the JVM.</summary>
    [Fact]
    public void PatchSkipsBytesThatArentAClassFile()
    {
        ClassPatchResult skipped = PatcherAgent.TweakSort.Apply(new byte[16]);

        Assert.False(skipped.Changed);
        Assert.Contains(expectedSubstring: "not a valid class file", skipped.SkippedReason, StringComparison.Ordinal);
    }

    // A class file with straight-line methods, each of which calls Collections.sort or just returns.
    private static byte[] CreateClassFile(params (string Name, bool CallsSort)[] methods)
    {
        JavaMethod sort = JavaMethod.Static(owner: "java/util/Collections", name: "sort", SortDescriptor);
        ClassFileWriter writer = new(ClassName);

        foreach ((string name, bool callsSort) in methods)
        {
            writer = writer.AddMethod(name,
                descriptor: "()V",
                code => (callsSort ? code.AConstNull().AConstNull().Invoke(sort) : code).Return());
        }

        return writer.ToArray();
    }
}
