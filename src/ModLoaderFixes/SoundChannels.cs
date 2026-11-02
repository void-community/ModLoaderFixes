using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Releases a Minecraft client's sound channels on the sound engine's thread. When the client leaves a world, <c>SoundEngine.stopAll</c> flushes the sound
/// executor, which starts a new sound thread, and then calls <c>ChannelAccess.clear</c>. That releases every channel on the Render thread, and a channel's release
/// closes its <c>OggAudioStream</c>, which frees the stb_vorbis decoder and its buffer. A streamed sound that a worker thread finished opening just before has
/// meanwhile queued its first buffers on the new sound thread, and that thread can be decoding them as they are freed, which crashes the JVM in
/// <c>liblwjgl_stb.so</c>. With <c>clear</c> handed to the sound executor, as <c>ChannelAccess</c>'s other methods hand their work, and waited for, a stream is only
/// closed between two of its reads. The executor runs a task at once when it is already on its own thread. Minecraft 1.14 to 1.20.4 decode with stb_vorbis; 1.20.5
/// moved to JOrbis and to Java 21, so classes compiled for Java 21 are left alone.
/// </summary>
internal static class SoundChannels
{
    private const string Description = "channels released on the sound thread";
    private const string Executor = "Ljava/util/concurrent/Executor;";
    // Java 21's class file version, which Minecraft 1.20.5 and later compile for.
    private const int Java21 = 65;

    private const string MovedBody = "modloaderfixes$clear";

    private const int Static = 0x0008;

    private static readonly JavaMethod ForEach = JavaMethod.Interface(owner: "java/util/Set", name: "forEach", descriptor: "(Ljava/util/function/Consumer;)V");
    private static readonly JavaMethod Clear = JavaMethod.Interface(owner: "java/util/Set", name: "clear", descriptor: "()V");
    private static readonly JavaMethod Run = JavaMethod.Interface(owner: "java/lang/Runnable", name: "run", descriptor: "()V");

    private static readonly JavaMethod RunAsync = JavaMethod.Static(
        owner: "java/util/concurrent/CompletableFuture",
        name: "runAsync",
        descriptor: "(Ljava/lang/Runnable;Ljava/util/concurrent/Executor;)Ljava/util/concurrent/CompletableFuture;"
    );

    private static readonly JavaMethod Join = JavaMethod.Virtual(owner: "java/util/concurrent/CompletableFuture", name: "join", descriptor: "()Ljava/lang/Object;");

    /// <summary>
    /// Gets the patches of <c>ChannelAccess</c>: Forge's, by MCP's name <c>ChannelManager</c> up to 1.16.5 and by Mojang's from 1.17, and vanilla's under each
    /// release's names up to 1.20.4.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [.. MappedClasses.ChannelManager.Patches(Defer), .. MappedClasses.ChannelAccess.Patches(Defer)];

    private static ClassPatch Defer(ClassNames names)
    {
        return names.Patch().Edit(Description, static context => Defer(context.Editor));
    }

    // clear is the one method without parameters that releases each channel with forEach and then empties the set, under whichever names the release gives it,
    // and the executor is the class's one field of its type. A class that has neither, has been patched already or is compiled for Java 21 is left alone.
    private static int Defer(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;
        ClassMember[] clears = [.. model.Methods.Where(method => method is { Descriptor: "()V", Code: { } code } && (method.AccessFlags & Static) == 0 && Releases(model, code))];
        ClassMember[] executors = [.. model.Fields.Where(static field => field is { Descriptor: Executor } && (field.AccessFlags & Static) == 0)];
        bool isPatched = model.FindMethod(MovedBody, descriptor: "()V") is not null;

        if (clears is not [{ } clear] || executors is not [{ } executor] || isPatched || model.MajorVersion >= Java21)
            return 0;

        JavaMethod moved = JavaMethod.Virtual(model.Name, MovedBody, descriptor: "()V");

        editor.ReplaceMethodBody(
            clear.Name,
            clear.Descriptor,
            MovedBody,
            code => code.ALoad(slot: 0)
                .Lambda(Run, moved)
                .ALoad(slot: 0)
                .GetField(model.Name, executor.Name, Executor)
                .Invoke(RunAsync)
                .Invoke(Join)
                .Pop()
                .Return()
        );

        return 1;
    }

    private static bool Releases(JavaClassFile model, CodeAttribute code)
    {
        return code.Instructions.Any(instruction => model.Invokes(instruction, ForEach)) && code.Instructions.Any(instruction => model.Invokes(instruction, Clear));
    }
}
