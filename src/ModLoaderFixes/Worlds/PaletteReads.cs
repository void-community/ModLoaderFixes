using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Worlds;

/// <summary>
/// Makes a Minecraft 1.18 and later <c>PalettedContainer</c> read a block again when the block's palette entry isn't there yet. A chunk section keeps its blocks
/// as indexes into a palette. <c>set</c> adds a new kind of block to the palette, with plain writes to the palette's array and its size, and then stores the
/// kind's index; <c>get</c> reads the index and looks it up, and neither takes a lock, although the light engine reads the blocks of chunks that world
/// generation still sets. On ARM a thread can see the index before the entry, so the lookup throws <c>MissingPaletteEntryException</c>, which those releases
/// have, or gives null. The exception ends the light engine's task for the chunk, the chunk never gets its light, and a server that prepares its spawn area
/// stays at a percentage forever. In every release from 1.18 to 26.3 two threads on an ARM64 machine get the exception within a second; the releases from 1.13
/// to 1.17.1 answer such a read with the container's default block and go on. The patch moves the body of <c>get(int)</c> into a method of its own and has
/// <c>get</c> hand that to <c>modloaderfixes.Palettes</c>, which reads again while the read throws or gives null: the entry is on its way whenever the index is
/// there. It is found by its shape, which is the same in every release and under every naming. Paper from 1.21.3 looks the index up through a method it
/// adds: up to 26.2 one of the container, which reads the palette's own array and throws <c>IllegalArgumentException: Palette index out of bounds</c> for an
/// entry that is still null, and from 26.3 a static one, which asks the palette as Mojang's read does. Each gives <c>get</c> a shape of its own, and <c>get</c>
/// gets the same body.
/// </summary>
internal static class PaletteReads
{
    private const string Description = "blocks read again while their palette entry is on its way";
    private const string Descriptor = "(I)Ljava/lang/Object;";
    private const string MovedBody = "modloaderfixes$get";
    private const int Static = 0x0008;

    private static readonly JavaMethod Apply = JavaMethod.Interface(owner: "java/util/function/IntFunction", name: "apply", Descriptor);

    private static readonly JavaMethod Read = JavaMethod.Static(owner: "modloaderfixes/Palettes", name: "read", descriptor: "(Ljava/util/function/IntFunction;I)Ljava/lang/Object;");

    // get(int) as Paper compiles it from 1.21.3 to 26.2: Data data = this.data; return this.readPalette(data, data.storage.get(index));
    private static readonly byte[] PaperShape =
    [
        Opcodes.ALoad0, Opcodes.GetField, Opcodes.AStore2, Opcodes.ALoad0, Opcodes.ALoad2, Opcodes.ALoad2, Opcodes.GetField, Opcodes.ILoad1, Opcodes.InvokeInterface,
        Opcodes.InvokeVirtual, Opcodes.AReturn,
    ];

    // get(int) as Paper compiles it from 26.3: Data data = this.data; return readPalette(data, data.storage.get(index));
    private static readonly byte[] PaperStaticShape =
    [
        Opcodes.ALoad0, Opcodes.GetField, Opcodes.AStore2, Opcodes.ALoad2, Opcodes.ALoad2, Opcodes.GetField, Opcodes.ILoad1, Opcodes.InvokeInterface, Opcodes.InvokeStatic,
        Opcodes.AReturn,
    ];

    // get(int) as Mojang compiles it from 1.18: Data data = this.data; return data.palette.valueFor(data.storage.get(index));
    private static readonly byte[] Shape =
    [
        Opcodes.ALoad0, Opcodes.GetField, Opcodes.AStore2, Opcodes.ALoad2, Opcodes.GetField, Opcodes.ALoad2, Opcodes.GetField, Opcodes.ILoad1, Opcodes.InvokeInterface,
        Opcodes.InvokeInterface, Opcodes.AReturn,
    ];

    /// <summary>
    /// Gets the patches of the container: by Mojang's name, which Forge, NeoForge, Paper from 1.20.5 and Minecraft from 26.1 run, by each obfuscated name from
    /// 1.18 to 1.21.11, and by the name Spigot gives it, which Paper runs up to 1.20.4.
    /// </summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. MappedClasses.PalettedContainer.Patches(static names => names.Patch().Edit(Description, static context => ReadAgain(context.Editor))),
        ClassPatch.For(className: "net/minecraft/world/level/chunk/DataPaletteBlock").Edit(Description, static context => ReadAgain(context.Editor)),
    ];

    private static bool IsShaped(CodeAttribute code)
    {
        byte[] opcodes = [.. code.Instructions.Select(static instruction => instruction.Opcode)];

        return opcodes.SequenceEqual(Shape) || opcodes.SequenceEqual(PaperShape) || opcodes.SequenceEqual(PaperStaticShape);
    }

    // get(int) is the one instance method that takes an int, returns an object and has one of the shapes above. A class without it, as another release's class of the
    // same obfuscated name, or one patched already, is left alone.
    private static int ReadAgain(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;
        ClassMember[] reads = [.. model.Methods.Where(method => method is { Descriptor: Descriptor, Code: { } code } && (method.AccessFlags & Static) == 0 && IsShaped(code))];

        if (reads is not [var read] || model.FindMethod(MovedBody, Descriptor) is not null)
            return 0;

        JavaMethod moved = JavaMethod.Virtual(model.Name, MovedBody, Descriptor);

        editor.ReplaceMethodBody(read.Name, Descriptor, MovedBody, code => code.ALoad(slot: 0).Lambda(Apply, moved).ILoad(slot: 1).Invoke(Read).AReturn());

        return 1;
    }
}
