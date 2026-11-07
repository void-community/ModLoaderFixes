using System.IO.Compression;

using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using ModLoaderFixes.Worlds;

using Xunit;

namespace ModLoaderFixes.Tests.Worlds;

/// <summary>Making the sampler of a dimension's environment attributes read its tick cache once, by Mojang's name and by 1.21.11's obfuscated one.</summary>
public sealed class AttributeSamplersPatchTests
{
    private const string ObjectDescriptor = "Ljava/lang/Object;";
    private const string Sampler = "net/minecraft/world/attribute/EnvironmentAttributeSystem$ValueSampler";
    private const string ValueDescriptor = "()Ljava/lang/Object;";

    /// <summary>A getter that already reads the cache once has another shape and is left alone.</summary>
    [Fact]
    public void PatchLeavesAGetterOfAnotherShape()
    {
        byte[] classFile = new ClassFileWriter(Sampler)
            .AddField(name: "cachedTickValue", ObjectDescriptor)
            .AddMethod(
                name: "getDimensionValue",
                ValueDescriptor,
                static code => code.ALoad(slot: 0).GetField(Sampler, name: "cachedTickValue", ObjectDescriptor).AReturn(),
                accessFlags: 0x0001
            )
            .ToArray();

        Assert.False(Apply(classFile).Changed);
    }

    /// <summary>
    /// In the real sampler of 1.21.11 and of 26.1, the getter reads the cache once and hands it to the helper with the method the patch adds, which computes the value
    /// and caches it. The samplers come from the pinned clients: a getter of their shape branches, which a class file that can hold the patch's lambda only allows
    /// with stack map frames, which ClassFileWriter doesn't write.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="sha1">The SHA-1 that Mojang serves its client under.</param>
    /// <param name="sha256">The client's pinned checksum.</param>
    /// <param name="className">The sampler's name in that client.</param>
    /// <param name="getter">The getter's name there.</param>
    /// <param name="compute">The name of the method that computes the value there.</param>
    /// <param name="cache">The name of the cache's field there.</param>
    [Theory]
    [InlineData(
        "1.21.11",
        "ba2df812c2d12e0219c489c4cd9a5e1f0760f5bd",
        "1473c9489ac50fda3c435049a76a70d61a10b8610db27f5ba9d8756b686cd3bd",
        "cef$b",
        "b",
        "c",
        "e"
    )]
    [InlineData(
        "26.1",
        "191771837687b766537a8c4607cb6fad79c533a1",
        "bc6194c61566b587f196090d730698f2191170f7580adb143e4ff839939d3840",
        Sampler,
        "getDimensionValue",
        "computeValueNotPositional",
        "cachedTickValue"
    )]
    public async Task PatchReadsTheCacheOnce(string version, string sha1, string sha256, string className, string getter, string compute, string cache)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string jar = await RealJarTests.VanillaClient(version, sha1, sha256).GetPathAsync(cancellationToken);
        byte[] classFile = await ReadClassAsync(jar, className, cancellationToken);

        ClassPatchResult result = Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);
        IReadOnlyList<Instruction> body = model.FindMethod(getter, ValueDescriptor)?.Code?.Instructions ?? [];
        Instruction read = Assert.Single(body, static instruction => instruction.Opcode == Opcodes.GetField);

        Assert.Equal(cache, model.GetMemberReference(read.ConstantIndex ?? 0).Name);
        ClassFileAssert.Calls(
            patched,
            getter,
            owner: "modloaderfixes/AttributeValues",
            name: "cachedOrComputed",
            "(" + ObjectDescriptor + "Ljava/util/function/Supplier;)" + ObjectDescriptor
        );
        ClassFileAssert.Calls(patched, method: "modloaderfixes$computeDimensionValue", className, compute, ValueDescriptor);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        return AttributeSamplers.Patches.Single(patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
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
