using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Rendering;

/// <summary>
/// Makes a Minecraft 1.14 client's entity render dispatcher draw an entity only once it has a camera. <c>EntityRendererManager.setWorld</c> clears the camera when
/// the client leaves a level, and it starts out without one; only <c>prepare</c>, which the level renderer's entity pass calls, sets it. That pass skips the first
/// two frames of each new level, which still draw their particles, and an item pickup particle draws its item through the dispatcher, whose renderer then asks how
/// far the item is from the camera: a pickup in those frames crashes the game with a NullPointerException in <c>Post-rendering entity in world</c>. Minecraft 1.15
/// prepares the dispatcher at the start of every frame. The patch moves the body of the dispatcher's <c>renderEntity</c> into a method of its own and gives it a
/// body that hands the camera and that method to <c>modloaderfixes.EntityRenders</c>, which runs it only when there is a camera.
/// </summary>
internal static class EntityRenderCameras
{
    private const string Description = "entities drawn only with a camera";
    private const string MovedBody = "modloaderfixes$renderEntity";
    private const string Render = "func_188391_a";
    private const string RenderDescriptor = "(Lnet/minecraft/entity/Entity;DDDFFZ)V";
    private const string SetWorld = "func_78717_a";
    private const string SetWorldDescriptor = "(Lnet/minecraft/world/World;)V";
    private const int Static = 0x0008;

    private static readonly JavaMethod RenderWithCamera = JavaMethod.Static(owner: "modloaderfixes/EntityRenders", name: "renderWithCamera", descriptor: "(Ljava/lang/Object;Ljava/lang/Runnable;)V");

    private static readonly JavaMethod Run = JavaMethod.Interface(owner: "java/lang/Runnable", name: "run", descriptor: "()V");

    // setWorld as Mojang compiles it in 1.14: this.world = world; if (world == null) this.camera = null.
    private static readonly byte[] SetWorldShape =
    [
        Opcodes.ALoad0, Opcodes.ALoad1, Opcodes.PutField, Opcodes.ALoad1, Opcodes.IfNonNull, Opcodes.ALoad0, Opcodes.AConstNull, Opcodes.PutField, Opcodes.Return,
    ];

    /// <summary>Gets the patches of the dispatcher: Forge's, by MCP's name <c>EntityRendererManager</c>, and vanilla's under each release's names from 1.14 to 1.14.4.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [.. MappedClasses.EntityRendererManager.Patches(Guard)];

    private static ClassPatch Guard(ClassNames names)
    {
        JavaMethod render = names.Virtual(Render, RenderDescriptor);
        JavaMethod setWorld = names.Virtual(SetWorld, SetWorldDescriptor);

        return names.Patch().Edit(Description, context => Guard(context.Editor, render, setWorld));
    }

    // The camera is the field that setWorld clears when the world is null, which it finds of the shape above; renderEntity is found by its name and descriptor. A
    // class without either, or patched already, is left alone.
    private static int Guard(ClassFileEditor editor, JavaMethod render, JavaMethod setWorld)
    {
        JavaClassFile model = editor.Model;
        ClassMember? renders = model.FindMethod(render.Name, render.Descriptor);
        IReadOnlyList<Instruction>? clears = model.FindMethod(setWorld.Name, setWorld.Descriptor)?.Code?.Instructions;
        bool isPatched = model.FindMethod(MovedBody, render.Descriptor) is not null;
        bool isShaped = clears is not null && clears.Select(static instruction => instruction.Opcode).SequenceEqual(SetWorldShape);

        if (renders is null || (renders.AccessFlags & Static) != 0 || clears is null || !isShaped || isPatched)
            return 0;

        MemberReference camera = model.GetMemberReference(clears[index: 7].ConstantIndex ?? 0);

        if (camera.Owner != model.Name || !camera.Descriptor.StartsWith(value: 'L'))
            return 0;

        JavaMethod moved = JavaMethod.Virtual(model.Name, MovedBody, render.Descriptor);

        // The lambda captures the dispatcher and each argument, in the slots that the descriptor (entity, x, y, z, yaw, partial ticks, debug) gives them.
        editor.ReplaceMethodBody(
            render.Name,
            render.Descriptor,
            MovedBody,
            code => code.ALoad(slot: 0)
                .GetField(model.Name, camera.Name, camera.Descriptor)
                .ALoad(slot: 0)
                .ALoad(slot: 1)
                .DLoad(slot: 2)
                .DLoad(slot: 4)
                .DLoad(slot: 6)
                .FLoad(slot: 8)
                .FLoad(slot: 9)
                .ILoad(slot: 10)
                .Lambda(Run, moved)
                .Invoke(RenderWithCamera)
                .Return()
        );

        return 1;
    }
}
