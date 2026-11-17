using System.Globalization;
using System.Net;
using System.Net.Sockets;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers.Local;

/// <summary>
/// A real server laid out in a directory, to run as a process of the platform the tests run on, with that platform's own JDK and agent. The servers in itzg's
/// container are 64-bit Linux with glibc on every machine, so only these meet the library of Windows, macOS, Alpine and 32-bit ARM. Each file is pinned by its
/// checksum. A server before Forge 1.13 is its files, laid out where its installer would put them, and the installer isn't run: it asks a mirror it picks from a
/// list, once, and fails when a connection does. A Forge from 1.13 can't be laid out that way, as its installer makes the server's jars from Minecraft's on the
/// machine, so its one file is the installer, which is run, again when a download failed, and checks every file it downloads itself.
/// </summary>
/// <param name="Java">The Java the server runs on, which decides the platforms that can run it.</param>
/// <param name="MainClass">The class that the first file starts: the server, or the installer when the server has to be installed.</param>
/// <param name="WorldType">
/// How the server's properties name a flat world, which a slow machine generates in seconds; an Intel Mac of CI didn't generate 1.18.2's default world in five minutes.
/// </param>
/// <param name="Files">The server's files; the first is the jar that <paramref name="MainClass"/> is started from.</param>
/// <param name="ArgumentFiles">
/// The directory, in the server's, where the installer leaves <c>unix_args.txt</c> and <c>win_args.txt</c>, which say how the server starts, or <see langword="null"/>
/// for a server that needs no installer.
/// </param>
internal sealed record LocalServer(int Java, string MainClass, string WorldType, IReadOnlyList<LocalServerFile> Files, string? ArgumentFiles = null)
{
    private const string ForgeMaven = "https://maven.minecraftforge.net/";
    private const int InstallAttempts = 3;
    private const string Libraries = "https://libraries.minecraft.net/";

    /// <summary>
    /// Gets Forge 1.20.2-48.1.0, which runs on Java 17, so on every platform. Its installer downloads 75 libraries, Minecraft's server and Mojang's mappings, and
    /// takes minutes to split, rename and patch Minecraft's jar. The server starts from the argument file the installer writes, by the launcher's class on a
    /// module path.
    /// </summary>
    internal static LocalServer Forge1202 { get; } = new(
        Java: 17,
        MainClass: "net.minecraftforge.installer.SimpleInstaller",
        WorldType: "flat",
        [
            new(
                Path: "forge-1.20.2-48.1.0-installer.jar",
                ForgeMaven + "net/minecraftforge/forge/1.20.2-48.1.0/forge-1.20.2-48.1.0-installer.jar",
                Sha256: "10c3f124c8c6b9c2550e0003ebf014c8d24f8ef6b4b9d76fd4619cdc6013eef6"
            ),
        ],
        ArgumentFiles: "libraries/net/minecraftforge/forge/1.20.2-48.1.0"
    );

    /// <summary>
    /// Gets Forge 1.7.2-10.12.2.1161, which runs on Java 8, so everywhere but on Windows for ARM64. Forge's jar names the six libraries and the vanilla server in
    /// its manifest's class path by their paths beside it. Without the agent it ends with a <c>ConcurrentModificationException</c> as it sorts its tweakers.
    /// </summary>
    internal static LocalServer Forge172 { get; } = new(
        Java: 8,
        MainClass: "cpw.mods.fml.relauncher.ServerLaunchWrapper",
        WorldType: "FLAT",
        [
            new(
                Path: "forge-1.7.2-10.12.2.1161-mc172-universal.jar",
                ForgeMaven + "net/minecraftforge/forge/1.7.2-10.12.2.1161-mc172/forge-1.7.2-10.12.2.1161-mc172-universal.jar",
                Sha256: "17628bcf403e312f8de275633528c171795fd0b5469ba9e496baf401a2cd1b7e"
            ),
            new(
                Path: "minecraft_server.1.7.2.jar",
                Address: "https://launcher.mojang.com/v1/objects/3716cac82982e7c2eb09f83028b555e9ea606002/server.jar",
                Sha256: "b4139899700c1bdbf72880eec4bdb9e46c2cf22d1724a48a018ee0330035462e"
            ),
            Library(
                Libraries,
                path: "net/minecraft/launchwrapper/1.9/launchwrapper-1.9.jar",
                sha256: "8bb37fe90579df168a1d4d950cca87ea4299038045470ac0efe61bc4e6873ede"
            ),
            Library(
                Libraries,
                path: "org/ow2/asm/asm-all/4.1/asm-all-4.1.jar",
                sha256: "165b583b0b548405dd97019666f94a86ee1d1e3af227ae4dc82c6e27d6885bb1"
            ),
            Library(
                ForgeMaven,
                path: "org/scala-lang/scala-library/2.10.2/scala-library-2.10.2.jar",
                sha256: "9b49ec36ccdea13fbca8d1e1e2a74a8cc94e4fa0a9aced1fc74f8df32c1b1b99"
            ),
            Library(
                ForgeMaven,
                path: "org/scala-lang/scala-compiler/2.10.2/scala-compiler-2.10.2.jar",
                sha256: "019ae7f9bb54f08d7d8057fb6380c4bf8faaa878e501567913dcb3a3c0f95d9f"
            ),
            Library(
                Libraries,
                path: "net/sf/jopt-simple/jopt-simple/4.5/jopt-simple-4.5.jar",
                sha256: "64335a48be6b142a7d0c13ec5c82a707857aa58c0234e6eda3eaca0e96eea51b"
            ),
            Library(Libraries, path: "lzma/lzma/0.0.1/lzma-0.0.1.jar", sha256: "0c3085cb93c3ef95d731c274b6f2dd195aa0405fe9328d69cab7f5e899d5e124"),
        ]
    );

    /// <summary>
    /// Gets vanilla Minecraft 1.18.2, which runs on Java 17, the oldest Java that every platform has, Windows for ARM64 among them. Its one jar holds its libraries
    /// and unpacks them into the directory it runs in. It logs one error about the flat world's settings, which its properties can't give it, and generates the
    /// default flat world.
    /// </summary>
    internal static LocalServer Vanilla1182 { get; } = new(
        Java: 17,
        MainClass: "net.minecraft.bundler.Main",
        WorldType: "flat",
        [
            new(
                Path: "server.jar",
                Address: "https://piston-data.mojang.com/v1/objects/c8f83c5655308435b3dcf03c06d9fe8740a77469/server.jar",
                Sha256: "57be9d1e35aa91cfdfa246adb63a0ea11a946081e0464d08bc3d36651718a343"
            ),
        ]
    );

    /// <summary>
    /// Puts the server's files into a directory, with the agreement to Mojang's licence that servers from 1.7.10 ask for and properties that have the server listen
    /// on a free port of this machine only and generate a flat world, and runs the installer when the server has one.
    /// </summary>
    /// <param name="directory">The empty directory to install into, which the server then runs in.</param>
    /// <param name="jdk">The JDK that runs the installer.</param>
    /// <param name="cancellationToken">Cancels the downloads and the installer.</param>
    /// <returns>How the server starts.</returns>
    internal async Task<LocalServerLaunch> InstallAsync(DirectoryInfo directory, Jdk jdk, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(jdk);

        foreach (LocalServerFile file in Files)
        {
            string cached = await new PinnedArtifact(new Uri(file.Address), file.Sha256).GetPathAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            FileInfo installed = new(Path.Combine(directory.FullName, file.Path));

            installed.Directory?.Create();
            File.Copy(cached, installed.FullName);
        }

        string[] properties = ["server-ip=127.0.0.1", "server-port=" + FreePort().ToString(CultureInfo.InvariantCulture), "online-mode=false", "level-type=" + WorldType];

        await File.WriteAllLinesAsync(Path.Combine(directory.FullName, path2: "server.properties"), properties, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(Path.Combine(directory.FullName, path2: "eula.txt"), contents: "eula=true\n", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        string jar = Path.Combine(directory.FullName, Files[index: 0].Path);

        if (ArgumentFiles is null)
            return new LocalServerLaunch(directory.FullName, MainClass, jar, [], []);

        await RunInstallerAsync(jdk, jar, directory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        string arguments = Path.Combine(directory.FullName, ArgumentFiles, OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt");

        return LocalServerLaunch.Parse(await File.ReadAllTextAsync(arguments, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), directory.FullName);
    }

    // A port that was free a moment ago: the system picks one for a listener that is closed again at once.
    private static int FreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, port: 0);

        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static LocalServerFile Library(string repository, string path, string sha256)
    {
        return new LocalServerFile("libraries/" + path, repository + path, sha256);
    }

    // The installer downloads each library once, from a mirror it picks, and gives up when a download fails. Run again it keeps what it has, so a few runs get
    // past a connection that broke, as itzg's image installs a server again when the container is started again.
    private async Task RunInstallerAsync(Jdk jdk, string installer, DirectoryInfo directory, CancellationToken cancellationToken)
    {
        JavaRunOptions options = new() { WorkingDirectory = directory.FullName, Arguments = ["--installServer"], Timeout = TimeSpan.FromMinutes(minutes: 20) };
        JavaRun installed = await jdk.RunAsync(MainClass, installer, options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        for (int attempt = 2; attempt <= InstallAttempts && installed.ExitCode != 0; attempt++)
            installed = await jdk.RunAsync(MainClass, installer, options: options, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        string[] lines = installed.Output.Split(separator: '\n');

        Assert.True(
            installed.ExitCode == 0,
            $"The installer failed {InstallAttempts} times. Its last output ends with:\n{string.Join(separator: '\n', lines.Skip(Math.Max(val1: 0, lines.Length - 40)))}"
        );
    }
}
