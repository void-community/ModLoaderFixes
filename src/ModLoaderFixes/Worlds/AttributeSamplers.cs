using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes.Worlds;

/// <summary>
/// Makes the value of a dimension's environment attribute come from one read of its tick cache. <c>EnvironmentAttributeSystem.ValueSampler.getDimensionValue</c>
/// checks <c>cachedTickValue</c> and reads it again to return it, while <c>ServerLevel.tick</c> clears it on the server thread at the start of each tick. The world
/// generation workers read the moon phase through it as they spawn a new chunk's mobs, so one that reads between the two gets null, and the server crashes with
/// <c>Exception generating new chunk</c> from a NullPointerException in <c>getMoonBrightness</c>. Minecraft has the class from 1.21.11, and Forge, NeoForge and
/// Paper keep it unchanged. The patch adds a method that computes the value and caches it, as the cache miss does, and gives <c>getDimensionValue</c> a body
/// that reads the field once and hands it with that method to <c>modloaderfixes.AttributeValues</c>.
/// </summary>
internal static class AttributeSamplers
{
    private const string Compute = "modloaderfixes$computeDimensionValue";
    private const string Description = "dimension values read from the cache once";
    private const string MovedBody = "modloaderfixes$getDimensionValue";
    private const string ObjectDescriptor = "Ljava/lang/Object;";
    private const int PrivateSynthetic = 0x1002;
    private const int Static = 0x0008;
    private const string ValueDescriptor = "()Ljava/lang/Object;";

    private static readonly JavaMethod CachedOrComputed = JavaMethod.Static(
        owner: "modloaderfixes/AttributeValues",
        name: "cachedOrComputed",
        descriptor: "(Ljava/lang/Object;Ljava/util/function/Supplier;)Ljava/lang/Object;"
    );

    // getDimensionValue as Mojang compiles it from 1.21.11: if (cachedTickValue != null) return cachedTickValue; else compute, cache and return the result.
    private static readonly byte[] Shape =
    [
        Opcodes.ALoad0, Opcodes.GetField, Opcodes.IfNull, Opcodes.ALoad0, Opcodes.GetField, Opcodes.AReturn, Opcodes.ALoad0, Opcodes.InvokeVirtual, Opcodes.AStore1,
        Opcodes.ALoad0, Opcodes.ALoad1, Opcodes.PutField, Opcodes.ALoad1, Opcodes.AReturn,
    ];

    private static readonly JavaMethod SupplierGet = JavaMethod.Interface(owner: "java/util/function/Supplier", name: "get", ValueDescriptor);

    /// <summary>Gets the patches of the sampler: by Mojang's name, which Forge, NeoForge, Paper and Minecraft from 26.1 run, and by 1.21.11's obfuscated one.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } = [.. MappedClasses.AttributeSampler.Patches(static names => names.Patch().Edit(Description, static context => ReadOnce(context.Editor)))];

    private static bool IsShaped(CodeAttribute code)
    {
        return code.Instructions.Select(static instruction => instruction.Opcode).SequenceEqual(Shape);
    }

    // The member is the class's own, of the descriptor, and each other instruction names the same member.
    private static bool Names(JavaClassFile model, MemberReference member, string descriptor, params Instruction[] others)
    {
        return member.Owner == model.Name && member.Descriptor == descriptor && others.All(
            other => other.ConstantIndex is { } index && model.GetMemberReference(index) is { } reference && reference.Name == member.Name && reference.Owner == member.Owner
        );
    }

    // getDimensionValue is the one method of the shape above, whose reads and write name the same field of the class, and whose miss calls a method of the class
    // that takes nothing and returns the value. A class without it, or patched already, is left alone.
    private static int ReadOnce(ClassFileEditor editor)
    {
        JavaClassFile model = editor.Model;
        ClassMember[] getters = [.. model.Methods.Where(method => method is { Descriptor: ValueDescriptor, Code: { } code } && (method.AccessFlags & Static) == 0 && IsShaped(code))];
        bool isPatched = model.FindMethod(Compute, ValueDescriptor) is not null;

        if (getters is not [{ Code.Instructions: var code } getter] || isPatched)
            return 0;

        MemberReference cache = model.GetMemberReference(code[index: 1].ConstantIndex ?? 0);
        MemberReference computed = model.GetMemberReference(code[index: 7].ConstantIndex ?? 0);

        if (!Names(model, cache, ObjectDescriptor, code[index: 4], code[index: 11]) || !Names(model, computed, ValueDescriptor))
            return 0;

        JavaMethod compute = JavaMethod.Virtual(model.Name, computed.Name, ValueDescriptor);
        JavaMethod computeAndCache = JavaMethod.Virtual(model.Name, Compute, ValueDescriptor);

        editor.AddMethod(
            PrivateSynthetic,
            Compute,
            ValueDescriptor,
            builder => builder.ALoad(slot: 0)
                .Invoke(compute)
                .AStore(slot: 1)
                .ALoad(slot: 0)
                .ALoad(slot: 1)
                .PutField(model.Name, cache.Name, ObjectDescriptor)
                .ALoad(slot: 1)
                .AReturn()
        );
        editor.ReplaceMethodBody(
            getter.Name,
            ValueDescriptor,
            MovedBody,
            code => code.ALoad(slot: 0)
                .GetField(model.Name, cache.Name, ObjectDescriptor)
                .ALoad(slot: 0)
                .Lambda(SupplierGet, computeAndCache)
                .Invoke(CachedOrComputed)
                .AReturn()
        );

        return 1;
    }
}
