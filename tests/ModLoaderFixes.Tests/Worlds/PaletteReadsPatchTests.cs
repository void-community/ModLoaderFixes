using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using ModLoaderFixes.Worlds;

using Xunit;

namespace ModLoaderFixes.Tests.Worlds;

/// <summary>
/// Making a chunk section's container read a block again while the block's palette entry is on its way: by Mojang's name, by Spigot's, and in each shape
/// that Mojang and Paper give the read.
/// </summary>
public sealed class PaletteReadsPatchTests
{
    private const string Container = "net/minecraft/world/level/chunk/PalettedContainer";
    private const string Data = "net/minecraft/world/level/chunk/PalettedContainer$Data";
    private const string Moved = "modloaderfixes$get";
    private const string Palette = "net/minecraft/world/level/chunk/Palette";
    private const int Protected = 0x0004;
    private const string Read = "(I)Ljava/lang/Object;";
    private const string SpigotContainer = "net/minecraft/world/level/chunk/DataPaletteBlock";
    private const string Storage = "net/minecraft/util/BitStorage";

    private static readonly JavaMethod Helper = JavaMethod.Static(owner: "modloaderfixes/Palettes", name: "read", descriptor: "(Ljava/util/function/IntFunction;I)Ljava/lang/Object;");
    private static readonly JavaMethod StorageGet = JavaMethod.Interface(Storage, name: "get", descriptor: "(I)I");

    /// <summary>A container whose read is patched already has the moved method, and a second pass leaves it as it is.</summary>
    [Fact]
    public void PatchLeavesAContainerItPatchedAlready()
    {
        byte[] patched = Apply(Vanilla(Container, read: "get")).Bytes ?? throw new InvalidOperationException(message: "The first pass changed nothing.");

        Assert.False(Apply(patched).Changed);
    }

    /// <summary>A read of another shape, here one that looks the index up in a palette of its own field, isn't the read of any release and is left alone.</summary>
    [Fact]
    public void PatchLeavesAReadOfAnotherShape()
    {
        byte[] classFile = new ClassFileWriter(Container)
            .AddField(name: "palette", "L" + Palette + ";")
            .AddMethod(
                name: "get",
                Read,
                static code => code.ALoad(slot: 0).GetField(Container, name: "palette", "L" + Palette + ";").ILoad(slot: 1).Invoke(JavaMethod.Interface(Palette, name: "valueFor", Read)).AReturn(),
                Protected
            )
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    /// <summary>
    /// In the real containers of 1.20.4, by its obfuscated name, and of 26.1, by Mojang's, the read hands its moved code to the helper. The classes come from the
    /// pinned clients, which ship the container as the servers do.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves its client under.</param>
    /// <param name="sha256">The client's pinned checksum.</param>
    /// <param name="className">The container's name in that client.</param>
    /// <param name="read">The name of its read there.</param>
    [Theory]
    [InlineData(
        "1.20.4",
        "fd19469fed4a4b4c15b2d5133985f0e3e7816a8a",
        "9221ab461a491bf9661cd8e773a5e662aaa43d600fa7970b8c12bbfb0431b838",
        "dlw",
        "a"
    )]
    [InlineData(
        "26.1",
        "191771837687b766537a8c4607cb6fad79c533a1",
        "bc6194c61566b587f196090d730698f2191170f7580adb143e4ff839939d3840",
        Container,
        "get"
    )]
    public async Task PatchReadsAgainInARealContainer(string version, string sha1, string sha256, string className, string read)
    {
        string jar = await RealJarTests.VanillaClient(version, sha1, sha256).GetPathAsync(TestContext.Current.CancellationToken);
        ClassPatchResult result = Assert.Single(PaletteReads.Patches.SelectMany(patch => patch.ApplyToJar(jar)), static result => result.Changed);

        Assert.Equal(className, result.ClassName);
        AssertReadsAgain(result, className, read);
    }

    /// <summary>
    /// The read as Mojang compiles it from 1.18, under Mojang's name, which Forge, NeoForge, Paper from 1.20.5 and Minecraft from 26.1 run, and under Spigot's
    /// name with the obfuscated name of the method, which Paper runs up to 1.20.4.
    /// </summary>
    /// <param name="className">The container's name.</param>
    /// <param name="read">The name of its read.</param>
    [Theory]
    [InlineData(Container, "get")]
    [InlineData(SpigotContainer, "a")]
    public void PatchReadsAgainInMojangsShape(string className, string read)
    {
        AssertReadsAgain(Apply(Vanilla(className, read)), className, read);
    }

    /// <summary>
    /// Paper from 1.21.3 to 26.2 has the read call a method it adds, which looks the index up in the palette's array. The read gets the same body: it hands its moved
    /// code to the helper, which reads again while that throws or gives null.
    /// </summary>
    [Fact]
    public void PatchReadsAgainInPapersShape()
    {
        byte[] classFile = new ClassFileWriter(Container)
            .AddField(name: "data", "L" + Data + ";")
            .AddMethod(
                name: "get",
                Read,
                static code => code.ALoad(slot: 0)
                    .GetField(Container, name: "data", "L" + Data + ";")
                    .AStore(slot: 2)
                    .ALoad(slot: 0)
                    .ALoad(slot: 2)
                    .ALoad(slot: 2)
                    .GetField(Data, name: "storage", "L" + Storage + ";")
                    .ILoad(slot: 1)
                    .Invoke(StorageGet)
                    .Invoke(JavaMethod.Virtual(Container, name: "readPalette", "(L" + Data + ";I)Ljava/lang/Object;"))
                    .AReturn(),
                accessFlags: 0x0001
            )
            .ToArray();

        AssertReadsAgain(Apply(classFile), Container, read: "get");
    }

    /// <summary>
    /// Paper from 26.3 has the read call a static method it adds, which asks the palette. The read gets the same body.
    /// </summary>
    [Fact]
    public void PatchReadsAgainInPapersStaticShape()
    {
        byte[] classFile = new ClassFileWriter(Container)
            .AddField(name: "data", "L" + Data + ";")
            .AddMethod(
                name: "get",
                Read,
                static code => code.ALoad(slot: 0)
                    .GetField(Container, name: "data", "L" + Data + ";")
                    .AStore(slot: 2)
                    .ALoad(slot: 2)
                    .ALoad(slot: 2)
                    .GetField(Data, name: "storage", "L" + Storage + ";")
                    .ILoad(slot: 1)
                    .Invoke(StorageGet)
                    .Invoke(JavaMethod.Static(Container, name: "readPalette", "(L" + Data + ";I)Ljava/lang/Object;"))
                    .AReturn(),
                accessFlags: 0x0001
            )
            .ToArray();

        AssertReadsAgain(Apply(classFile), Container, read: "get");
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return PaletteReads.Patches.Single(patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
    }

    // The read, which the real containers overload by name, calls the helper with a lambda of its moved code, and the moved code is the read as it was.
    private static void AssertReadsAgain(ClassPatchResult result, string className, string read)
    {
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);
        IReadOnlyList<Instruction> body = model.FindMethod(read, Read)?.Code?.Instructions ?? [];
        Instruction call = Assert.Single(body, static instruction => instruction.Opcode == Opcodes.InvokeStatic);

        Assert.True(model.Invokes(call, Helper));
        Assert.Contains(body, static instruction => instruction.Opcode == Opcodes.InvokeDynamic);
        Assert.Contains(model.FindMethod(Moved, Read)?.Code?.Instructions ?? [], static instruction => instruction.Opcode == Opcodes.InvokeInterface);
        Assert.Equal(className, model.Name);
    }

    private static byte[] Vanilla(string className, string read)
    {
        return new ClassFileWriter(className)
            .AddField(name: "data", "L" + Data + ";")
            .AddMethod(
                read,
                Read,
                static code => code.ALoad(slot: 0)
                    .GetField(Container, name: "data", "L" + Data + ";")
                    .AStore(slot: 2)
                    .ALoad(slot: 2)
                    .GetField(Data, name: "palette", "L" + Palette + ";")
                    .ALoad(slot: 2)
                    .GetField(Data, name: "storage", "L" + Storage + ";")
                    .ILoad(slot: 1)
                    .Invoke(StorageGet)
                    .Invoke(JavaMethod.Interface(Palette, name: "valueFor", Read))
                    .AReturn(),
                Protected
            )
            .ToArray();
    }
}
