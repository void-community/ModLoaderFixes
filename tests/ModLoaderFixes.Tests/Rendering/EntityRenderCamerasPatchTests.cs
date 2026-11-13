using System.IO.Compression;

using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using ModLoaderFixes.Rendering;

using Xunit;

namespace ModLoaderFixes.Tests.Rendering;

/// <summary>Making Minecraft 1.14's entity render dispatcher draw an entity only once it has a camera, by MCP's name and by each 1.14 release's obfuscated one.</summary>
public sealed class EntityRenderCamerasPatchTests
{
    private const string Dispatcher = "net/minecraft/client/renderer/entity/EntityRendererManager";
    private const string Helper = "modloaderfixes/EntityRenders";
    private const string Moved = "modloaderfixes$renderEntity";
    private const string RenderDescriptor = "(Lnet/minecraft/entity/Entity;DDDFFZ)V";
    private const string WorldDescriptor = "Lnet/minecraft/world/World;";

    /// <summary>
    /// In the real dispatcher of each 1.14 naming, renderEntity reads the camera that setWorld clears, hands it with its moved body to the helper and calls nothing
    /// else, and the moved body still draws the entity. The dispatchers come from the pinned clients: setWorld branches, which a class file that can hold the
    /// patch's lambda only allows with stack map frames, which ClassFileWriter doesn't write.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves its client under.</param>
    /// <param name="sha256">The client's pinned checksum.</param>
    /// <param name="className">The dispatcher's name in that client.</param>
    /// <param name="entity">The entity's name there.</param>
    /// <param name="camera">The name of the camera's field there.</param>
    [Theory]
    [InlineData(
        "1.14",
        "7a762a59345c13af7d87111207a93f5a8607f6c0",
        "93907cbf0655aa5f10d049efa4d745f4250eecc5d0d5d1268046eec3144f323a",
        "dpf",
        "aif",
        "c"
    )]
    [InlineData(
        "1.14.1",
        "55ba86ddcbc3579397f41910463ffd4056e1e523",
        "7194d1326cf796f62ac55f3cb56f851c232895639ae27510f3207d5a73f91814",
        "dpi",
        "aii",
        "c"
    )]
    [InlineData(
        "1.14.3",
        "af100b34ec7ef2b8b9cf7775b544d21d690dddec",
        "4063062677433988e012fba53e42e4c21adb6919ac7f061f19f3c6e75b334e07",
        "dpm",
        "ail",
        "c"
    )]
    [InlineData(
        "1.14.4",
        "8c325a0c5bd674dd747d6ebaa4c791fd363ad8a9",
        "b3b2a798e2d67b566008fe4a03767ae2c7ff3f8c7ba6751e7b71fc7299672d0a",
        "dsa",
        "aio",
        "c"
    )]
    public async Task PatchDrawsOnlyWithACamera(string version, string sha1, string sha256, string className, string entity, string camera)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string jar = await RealJarTests.VanillaClient(version, sha1, sha256).GetPathAsync(cancellationToken);
        byte[] classFile = await ReadClassAsync(jar, className, cancellationToken);
        string descriptor = "(L" + entity + ";DDDFFZ)V";

        ClassPatchResult result = Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);
        IReadOnlyList<Instruction> body = model.FindMethod(name: "a", descriptor)?.Code?.Instructions ?? [];
        Instruction read = Assert.Single(body, static instruction => instruction.Opcode == Opcodes.GetField);
        Instruction call = Assert.Single(body, static instruction => instruction.Opcode == Opcodes.InvokeStatic);
        MemberReference called = model.GetMemberReference(call.ConstantIndex ?? 0);

        Assert.Equal(camera, model.GetMemberReference(read.ConstantIndex ?? 0).Name);
        Assert.Equal((Helper, "renderWithCamera", "(Ljava/lang/Object;Ljava/lang/Runnable;)V"), (called.Owner, called.Name, called.Descriptor));
        Assert.NotNull(model.FindMethod(Moved, descriptor));
    }

    /// <summary>A setWorld that doesn't clear the camera has another shape, and the dispatcher is left alone.</summary>
    [Fact]
    public void PatchLeavesADispatcherWhoseSetWorldKeepsTheCamera()
    {
        byte[] classFile = new ClassFileWriter(Dispatcher)
            .AddField(name: "field_217783_c", descriptor: "Lnet/minecraft/client/renderer/ActiveRenderInfo;")
            .AddField(name: "field_78722_g", WorldDescriptor)
            .AddMethod(
                name: "func_78717_a",
                "(" + WorldDescriptor + ")V",
                static code => code.ALoad(slot: 0).ALoad(slot: 1).PutField(Dispatcher, name: "field_78722_g", WorldDescriptor).Return(),
                accessFlags: 0x0001
            )
            .AddMethod(name: "func_188391_a", RenderDescriptor, static code => code.Return(), accessFlags: 0x0001)
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return EntityRenderCameras.Patches.Single(patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
    }

    private static async Task<byte[]> ReadClassAsync(string jar, string className, CancellationToken cancellationToken)
    {
        ZipArchive archive = await ZipFile.OpenReadAsync(jar, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        await using (archive.ConfigureAwait(continueOnCapturedContext: false))
        {
            ZipArchiveEntry entry = archive.GetEntry(className + ".class") ?? throw new InvalidOperationException(className + " isn't in " + jar + ".");
            Stream stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            using MemoryStream bytes = new();

            await using (stream.ConfigureAwait(continueOnCapturedContext: false))
                await stream.CopyToAsync(bytes, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            return bytes.ToArray();
        }
    }
}
