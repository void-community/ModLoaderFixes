using JvmBridge.Agents;

namespace ModLoaderFixes.Startup;

/// <summary>
/// Gives the early loading window of Forge and NeoForge clients 30 seconds instead of 10 to create the game's window. <c>DisplayWindow.initWindow</c> schedules a
/// watchdog before it tries each OpenGL version in turn, and the watchdog ends the game with <c>Timed out trying to setup the Game Window</c> once its delay
/// passes, although the window may still be on its way, as it often is on a busy machine or one that renders in software. Every Forge build from 1.20.1-47.0.19,
/// which added the watchdog, waits 10 seconds, as NeoForge does for 1.20.1 to 1.21.3, except 1.21.1 from 21.1.121, and for 1.21.4 up to 21.4.91-beta; FancyModLoader
/// waits 30 from 4.0.37 and 6.0.9. The watchdog's delay is the one long 10 in <c>initWindow</c> of every build that waits 10.
/// </summary>
internal static class WindowWatchdog
{
    /// <summary>Gets the patches of <c>DisplayWindow</c>: Forge's, which NeoForge for 1.20.1 keeps, and NeoForge's from 1.20.2.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. new[] { "net/minecraftforge/fml/earlydisplay/DisplayWindow", "net/neoforged/fml/earlydisplay/DisplayWindow" }.Select(
            static className => ClassPatch.For(className).Method(name: "initWindow", descriptor: "(Ljava/lang/String;)V").ReplaceConstant(oldValue: 10L, newValue: 30L)
        ),
    ];
}
