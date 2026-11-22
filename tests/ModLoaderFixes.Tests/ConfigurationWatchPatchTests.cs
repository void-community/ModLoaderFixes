using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Leaving NightConfig's autosave and autoreload off FML's config in Forge's FMLConfig.loadFrom.</summary>
public sealed class ConfigurationWatchPatchTests
{
    private const string Builder = "com/electronwill/nightconfig/core/file/GenericBuilder";
    private const string BuilderDescriptor = "()Lcom/electronwill/nightconfig/core/file/GenericBuilder;";
    private const string ClassName = "net/minecraftforge/fml/loading/FMLConfig";
    private const string LoadFrom = "loadFrom";

    /// <summary>Forge from 1.20.1 builds the config without either, so its loadFrom is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConfigsBuiltWithoutThem()
    {
        ClassPatchResult skipped = ModLoaderConfigurationWatch.Patch.Apply(CreateClassFile(builderCalls: ["sync"]));

        Assert.False(skipped.Changed);
        Assert.Contains("no invocation of " + Builder, skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Both calls go, so the builder they returned stays on the stack for the call that follows, which is kept.</summary>
    /// <exception cref="InvalidOperationException">
    /// The patch left FMLConfig unchanged although its loadFrom calls autosave and autoreload, so there is no patched loadFrom to look into; the message is the patch's
    /// own line about the class.
    /// </exception>
    [Fact]
    public void PatchLeavesOffAutosaveAndAutoreload()
    {
        ClassPatchResult applied = ModLoaderConfigurationWatch.Patch
            .Apply(CreateClassFile(builderCalls: ["autosave", "autoreload", "sync"]));

        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());

        Assert.Equal(expected: 2, applied.RemovedInvocations);
        ClassFileAssert.DoesNotCall(patched, LoadFrom, Builder, name: "autosave", BuilderDescriptor);
        ClassFileAssert.DoesNotCall(patched, LoadFrom, Builder, name: "autoreload", BuilderDescriptor);
        ClassFileAssert.Calls(patched, LoadFrom, Builder, name: "sync", BuilderDescriptor);
    }

    // A loadFrom that calls the given builder methods one after another on a null builder.
    private static byte[] CreateClassFile(string[] builderCalls)
    {
        return new ClassFileWriter(ClassName)
            .AddMethod(LoadFrom,
                descriptor: "(Ljava/nio/file/Path;)V",
                code => builderCalls.Aggregate(code.AConstNull(), static (code, call) => code.Invoke(JavaMethod.Virtual(Builder, call, BuilderDescriptor)))
                    .Pop()
                    .Return())
            .ToArray();
    }
}
