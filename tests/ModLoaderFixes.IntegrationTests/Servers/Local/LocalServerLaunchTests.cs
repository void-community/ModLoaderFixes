using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers.Local;

/// <summary>How the tests read the argument file that Forge's installer writes, which needs no server to check.</summary>
public sealed class LocalServerLaunchTests
{
    /// <summary>
    /// The file names the JVM's options, some with their value as the next word, then the launcher's class and its arguments. The options go before the class
    /// path, the class is started by its name and the arguments follow it, on Windows, whose file separates the module path's jars with semicolons, as elsewhere.
    /// </summary>
    [Fact]
    public void ArgumentFileIsReadAsOptionsClassAndArguments()
    {
        const string unix = "-p libraries/a.jar:libraries/b.jar --add-modules ALL-MODULE-PATH --add-opens java.base/java.util.jar=cpw.mods.securejarhandler "
            + "-DlibraryDirectory=libraries\nnet.minecraftforge.bootstrap.BootstrapLauncher --launchTarget forge_server --fml.forgeVersion 48.1.0\n";

        LocalServerLaunch launch = LocalServerLaunch.Parse(unix, directory: "/server");

        LocalServerLaunch windows = LocalServerLaunch.Parse(unix.Replace(oldChar: ':', newChar: ';')
            .Replace(oldValue: "\n", newValue: "\r\n", StringComparison.Ordinal),
            directory: "C:\\server");

        Assert.Equal(expected:
        [
            "-p",
            "libraries/a.jar:libraries/b.jar",
            "--add-modules",
            "ALL-MODULE-PATH",
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler",
            "-DlibraryDirectory=libraries",
        ],
            launch.JvmArguments);

        // The first word after the options is the class, the words after it are its arguments, and the server's directory stands in for the class path that the
        // file doesn't name.
        Assert.Equal(expected: "net.minecraftforge.bootstrap.BootstrapLauncher", launch.MainClass);
        Assert.Equal(expected: ["--launchTarget", "forge_server", "--fml.forgeVersion", "48.1.0"], launch.Arguments);
        Assert.Equal(expected: "/server", launch.ClassPath);

        // Windows' file keeps the semicolons of its module path in one option, and its line endings change neither the class nor the arguments.
        Assert.Equal(expected: "libraries/a.jar;libraries/b.jar", windows.JvmArguments[index: 1]);
        Assert.Equal(launch.MainClass, windows.MainClass);
        Assert.Equal(launch.Arguments, windows.Arguments);
    }

    /// <summary>A file that names no class can't start anything, and the failure shows what the file held.</summary>
    [Fact]
    public void ArgumentFileWithoutAClassIsRefused()
    {
        FormatException exception = Assert.Throws<FormatException>(static () => LocalServerLaunch.Parse(text: "-p libraries/a.jar --add-modules ALL-MODULE-PATH",
            directory: "/server"));

        Assert.Contains(expectedSubstring: "names no class to start", exception.Message, StringComparison.Ordinal);
    }
}
