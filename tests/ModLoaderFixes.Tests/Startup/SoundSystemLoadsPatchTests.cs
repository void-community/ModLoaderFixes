using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Making SoundManager's reload of the sound system wait for the loader thread its load started, by Forge's names and by vanilla's.</summary>
public sealed class SoundSystemLoadsPatchTests
{
    private const string Helper = "modloaderfixes/SoundLoaders";
    private const string ThreadClass = "java/lang/Thread";

    /// <summary>Another release's class under a manager's obfuscated name, whose method of the load's name starts no thread, is left alone.</summary>
    [Fact]
    public void PatchLeavesAnotherClassUnderAManagersName()
    {
        ClassPatchResult skipped = Apply(CreateManager(className: "btj",
            load: "i",
            reload: "a",
            unload: "b",
            startsLoader: false));

        Assert.False(skipped.Changed);
    }

    /// <summary>The load starts its loader through the helper, and the reload hands its manager to the helper's wait before it runs.</summary>
    /// <param name="className">The manager's internal name: Forge's <c>SoundManager</c>, or the obfuscated name that a vanilla release gives it.</param>
    /// <param name="load">The name that the manager's load of the sound system has there, the method that starts the loader thread.</param>
    /// <param name="reload">The name of its reload of the sound system, which unloads and then loads; the helper's wait has to come before it.</param>
    /// <param name="unload">
    /// The name of its unload; the reload's call of it is what tells the patch the reload from a method of the same name in another release's class.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No patch that matches <paramref name="className"/> changed the manager, so there is no patched class to look into; the message is the account of the
    /// last patch that matched.
    /// </exception>
    [Theory]
    [InlineData("net/minecraft/client/audio/SoundManager", "func_148608_i", "func_148596_a", "func_148613_b")]
    [InlineData("btj", "i", "a", "b")]
    [InlineData("dgr", "h", "a", "b")]
    public void PatchWaitsForTheLoaderBeforeTheReload(string className, string load, string reload, string unload)
    {
        ClassPatchResult applied = Apply(CreateManager(className, load, reload, unload, startsLoader: true));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        // The patch redirected one call, the start of the loader, and put its advice before one method, the reload.
        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        Assert.Equal(expected: 1, applied.AdvisedMethods);

        // The load hands its thread to the helper and no longer starts it itself, and the reload waits for the loader through the helper.
        ClassFileAssert.Calls(patched, load, Helper, name: "start", descriptor: "(Ljava/lang/Thread;)V");
        ClassFileAssert.DoesNotCall(patched, load, ThreadClass, name: "start", descriptor: "()V");
        ClassFileAssert.Calls(patched, reload, Helper, name: "awaitLoader", descriptor: "(Ljava/lang/Object;)V");
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        ClassPatchResult[] results = [.. SoundSystemLoads.Patches
            .Where(patch => patch.Matches(classFile, hierarchy: null))
            .Select(patch => patch.Apply(classFile))];

        Assert.NotEmpty(results);

        return results.FirstOrDefault(static result => result.Changed) ?? results[^1];
    }

    // A SoundManager whose load starts a loader thread, a null one here, and whose reload unloads and then loads, as Minecraft 1.7.10's does.
    private static byte[] CreateManager(string className, string load, string reload, string unload, bool startsLoader)
    {
        JavaMethod start = JavaMethod.Virtual(ThreadClass, name: "start", descriptor: "()V");

        return new ClassFileWriter(className).AddMethod(load, descriptor: "()V", code => (startsLoader ? code.AConstNull().Invoke(start) : code).Return(), accessFlags: 0x0022)
            .AddMethod(unload, descriptor: "()V", static code => code.Return(), accessFlags: 0x0001)
            .AddMethod(reload,
                descriptor: "()V",
                code => code.ALoad(slot: 0)
                    .Invoke(JavaMethod.Virtual(className, unload, descriptor: "()V"))
                    .ALoad(slot: 0)
                    .Invoke(JavaMethod.Virtual(className, load, descriptor: "()V"))
                    .Return(),
                accessFlags: 0x0001)
            .ToArray();
    }
}
