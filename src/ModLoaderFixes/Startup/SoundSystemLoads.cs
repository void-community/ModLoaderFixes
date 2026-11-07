using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Makes a Minecraft client's reload of its sound system wait for the sound system it is still loading. <c>SoundManager.loadSoundSystem</c> starts a "Sound
/// Library Loader" thread, which builds the sound system and only then sets <c>loaded</c>, and <c>reloadSoundSystem</c> unloads, which it does only once
/// <c>loaded</c> is set, and loads again. A reload during the load, as Forge's second resource reload at startup can be, starts a second loader beside the first.
/// Up to 1.12.2 LWJGL 2 has one OpenAL context, so the second sound system's <c>AL.create</c> fails, and once it gives up the first one's threads call OpenAL
/// functions that are no longer bound: the client crashes with <c>UnsatisfiedLinkError</c>. 1.13 to 1.13.2 load the same way with Mojang's OpenAL library for
/// LWJGL 3, which opens a second device for the second sound system and never cleans the first up. The patches point the loader's <c>Thread.start</c> at
/// <c>modloaderfixes.SoundLoaders</c>, which remembers the thread, and run the helper's wait for it before <c>reloadSoundSystem</c>.
/// </summary>
internal static class SoundSystemLoads
{
    private const string Helper = "modloaderfixes/SoundLoaders";
    private const string LoadSoundSystem = "func_148608_i";
    private const string ReloadSoundSystem = "func_148596_a";
    private const string UnloadSoundSystem = "func_148613_b";

    private static readonly JavaMethod Start = JavaMethod.Virtual(owner: "java/lang/Thread", name: "start", descriptor: "()V");

    /// <summary>Gets the patches of <c>SoundManager</c>, by MCP's name, which Forge runs from 1.7 to 1.13.2, and by each vanilla release's names.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [.. MappedClasses.SoundManager.Patches(Await)];

    // The load starts the loader thread, and the reload calls the unload, which another release's class of the same obfuscated name doesn't. The reload calls the
    // load too, but the load is private, so through invokespecial, which a body condition doesn't name.
    private static ClassPatch Await(ClassNames names)
    {
        JavaMethod load = names.Virtual(LoadSoundSystem, descriptor: "()V");
        JavaMethod reload = names.Virtual(ReloadSoundSystem, descriptor: "()V");
        JavaMethod unload = names.Virtual(UnloadSoundSystem, descriptor: "()V");

        return names.Patch()
            .Method(load.Name, load.Descriptor)
            .When(body => body.Calls(Start))
            .RedirectInvocation(Start, Helper)
            .Method(reload.Name, reload.Descriptor)
            .When(body => body.Calls(unload))
            .Before(JavaMethod.Static(Helper, name: "awaitLoader", descriptor: "(Ljava/lang/Object;)V"));
    }
}
