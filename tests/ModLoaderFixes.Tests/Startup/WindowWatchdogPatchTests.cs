using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;

using ModLoaderFixes.Startup;

using Xunit;

namespace ModLoaderFixes.Tests.Startup;

/// <summary>Giving the early loading window of Forge and NeoForge 30 seconds instead of 10 to create the game's window, in <c>DisplayWindow.initWindow</c>.</summary>
public sealed class WindowWatchdogPatchTests
{
    private const string FrameTime = "frameTime";
    private const string WindowSetup = "initWindow";
    private const string WindowSetupDescriptor = "(Ljava/lang/String;)V";

    private static readonly JavaMethod Schedule = JavaMethod.Interface(
        owner: "java/util/concurrent/ScheduledExecutorService",
        name: "schedule",
        descriptor: "(Ljava/lang/Runnable;JLjava/util/concurrent/TimeUnit;)Ljava/util/concurrent/ScheduledFuture;"
    );

    /// <summary>A window that waits 30 seconds already, as FancyModLoader's from 4.0.37 does, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesAWindowThatWaitsThirtySeconds()
    {
        ClassPatchResult result = Apply(className: "net/neoforged/fml/earlydisplay/DisplayWindow", watchdog: 30);

        Assert.False(result.Changed);
        Assert.NotNull(result.SkippedReason);
    }

    /// <summary>
    /// The watchdog waits 30 seconds under Forge's package and NeoForge's, while the render tick's delay before it and the 10 milliseconds of the minimum frame time,
    /// which another method loads, stay as they are.
    /// </summary>
    [Theory]
    [InlineData("net/minecraftforge/fml/earlydisplay/DisplayWindow")]
    [InlineData("net/neoforged/fml/earlydisplay/DisplayWindow")]
    public void PatchWaitsThirtySecondsForTheWindow(string className)
    {
        ClassPatchResult result = Apply(className, watchdog: 10);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.ReplacedConstants);
        Assert.Equal([50L, 30L], Longs(patched, WindowSetup, WindowSetupDescriptor));
        Assert.Equal([10L], Longs(patched, FrameTime, descriptor: "()J"));
    }

    private static ClassPatchResult Apply(string className, long watchdog)
    {
        byte[] classFile = CreateWindow(className, watchdog);

        return WindowWatchdog.Patches.Single(patch => patch.Matches(classFile, hierarchy: null)).Apply(classFile);
    }

    // An initWindow of Forge 1.20.1's shape, which schedules the render tick 50 milliseconds out and then the watchdog the given number of seconds out, and a method
    // that loads the same 10 for the minimum frame time, as the lambda that MINFRAMETIME's initializer compiles to does.
    private static byte[] CreateWindow(string className, long watchdog)
    {
        static CodeBuilder ScheduleIn(CodeBuilder code, long delay, string unit)
        {
            return code.ALoad(slot: 2)
                .AConstNull()
                .LConst(delay)
                .GetStatic(owner: "java/util/concurrent/TimeUnit", unit, descriptor: "Ljava/util/concurrent/TimeUnit;")
                .Invoke(Schedule)
                .Pop();
        }

        return new ClassFileWriter(className)
            .AddMethod(
                WindowSetup,
                WindowSetupDescriptor,
                code => ScheduleIn(ScheduleIn(code.AConstNull().AStore(slot: 2), delay: 50, unit: "MILLISECONDS"), watchdog, unit: "SECONDS").Return()
            )
            .AddMethod(FrameTime, descriptor: "()J", static code => code.LConst(value: 10).LReturn(), accessFlags: 0x0008)
            .ToArray();
    }

    // The long constants that a method loads, in order.
    private static List<long> Longs(byte[] classFile, string name, string descriptor)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);
        IReadOnlyList<Instruction> code = model.FindMethod(name, descriptor)?.Code?.Instructions ?? [];

        return [.. code.Select(instruction => instruction.ConstantIndex is { } index ? model.ConstantPool[index] : null).OfType<LongEntry>().Select(static entry => entry.Value)];
    }
}
