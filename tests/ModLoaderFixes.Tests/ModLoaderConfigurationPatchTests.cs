using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Pointing FML's load of its config at the helper that skips a second load.</summary>
public sealed class ModLoaderConfigurationPatchTests
{
    private const string Helper = "modloaderfixes/ModLoaderConfigs";
    private const string LoadFrom = "loadFrom";
    private const string LoadFromDescriptor = "(Ljava/nio/file/Path;)V";

    /// <summary>Gets the names of FML's configuration class in Forge and in NeoForge, with the type of its configData, which NeoForge's FML 4 changed.</summary>
    /// <value>
    /// One row per configuration class and type: the internal name of FMLConfig, then the descriptor of its <c>configData</c> field. NeoForge's class has two rows,
    /// one for each type that its configData has had.
    /// </value>
    public static TheoryData<string, string> Configurations { get; } = new()
    {
        { "net/minecraftforge/fml/loading/FMLConfig", "Lcom/electronwill/nightconfig/core/file/CommentedFileConfig;" },
        { "net/neoforged/fml/loading/FMLConfig", "Lcom/electronwill/nightconfig/core/file/CommentedFileConfig;" },
        { "net/neoforged/fml/loading/FMLConfig", "Lcom/electronwill/nightconfig/core/CommentedConfig;" },
    };

    /// <summary>A FMLConfig whose <c>load</c> doesn't call <c>loadFrom</c> isn't the code this fix was written for, so it is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConfigsWhoseLoadDoesNotCallLoadFrom()
    {
        ClassPatchResult skipped = Apply(CreateClassFile(className: "net/neoforged/fml/loading/FMLConfig",
            configurationDataDescriptor: "Ljava/lang/Object;",
            caller: "reload"));

        Assert.False(skipped.Changed);
        Assert.Contains(expectedSubstring: "load()V", skipped.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>load</c>'s call of <c>loadFrom</c> goes to the helper, with the config typed as FMLConfig, and the patch adds a getter of configData as an Object, whatever
    /// its type, and an invoker of the private loadFrom, which the helper calls.
    /// </summary>
    /// <param name="className">FMLConfig's name.</param>
    /// <param name="configurationDataDescriptor">The type of its configData.</param>
    /// <exception cref="InvalidOperationException">
    /// The patch of <paramref name="className"/> left a FMLConfig whose <c>load</c> calls <c>loadFrom</c> unchanged, so it has no bytes to check; the message is the
    /// patch's own line on why.
    /// </exception>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void PatchRedirectsTheLoadInLoad(string className, string configurationDataDescriptor)
    {
        ClassPatchResult applied = Apply(CreateClassFile(className, configurationDataDescriptor, caller: "load"));
        byte[] patched = applied.Bytes ?? throw new InvalidOperationException(applied.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);

        // The patch redirected one call and added two members.
        Assert.Equal(expected: 1, applied.RedirectedInvocations);
        Assert.Equal(expected: 2, applied.AddedMembers);

        // The call is load's of loadFrom, which now goes to the helper with the config as its first argument.
        ClassFileAssert.Calls(patched, method: "load", Helper, LoadFrom, "(L" + className + ";Ljava/nio/file/Path;)V");
        ClassFileAssert.DoesNotCall(patched, method: "load", className, LoadFrom, LoadFromDescriptor);

        // The members are the getter of configData, as an Object, and the invoker of the private loadFrom.
        Assert.NotNull(model.FindMethod(name: "modloaderfixes$configData", descriptor: "()Ljava/lang/Object;"));
        Assert.NotNull(model.FindMethod(name: "modloaderfixes$loadFrom", LoadFromDescriptor));
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string className = JavaClassFile.Read(classFile).Name;

        return ModLoaderConfigurationLoad.Patches.Single(patch => patch.ClassName == className).Apply(classFile);
    }

    // A class file with FML's private configData and loadFrom, whose load is empty unless it is the caller, and whose caller calls loadFrom on a null config with
    // a null path.
    private static byte[] CreateClassFile(string className, string configurationDataDescriptor, string caller)
    {
        JavaMethod loadFrom = JavaMethod.Virtual(className, LoadFrom, LoadFromDescriptor);

        ClassFileWriter writer = new ClassFileWriter(className).AddField(name: "configData", configurationDataDescriptor)
            .AddMethod(LoadFrom, LoadFromDescriptor, static code => code.Return(), accessFlags: 0x0002);

        if (caller != "load")
            writer = writer.AddMethod(name: "load", descriptor: "()V", static code => code.Return());

        return writer.AddMethod(caller, descriptor: "()V", code => code.AConstNull().AConstNull().Invoke(loadFrom).Return())
            .ToArray();
    }
}
