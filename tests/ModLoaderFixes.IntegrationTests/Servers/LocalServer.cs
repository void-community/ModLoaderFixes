using System.Globalization;
using System.Net;
using System.Net.Sockets;

using JvmBridge.Testing;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// A real server laid out in a directory, to run as a process of the platform the tests run on, with that platform's own JDK and agent. The servers in itzg's
/// container are 64-bit Linux with glibc on every machine, so only these meet the library of Windows, macOS, Alpine and 32-bit ARM. Each file is pinned by its
/// checksum and lies where the server's own installer puts it: Forge's installer asks a mirror it picks from a list, once, and fails when a connection does.
/// </summary>
/// <param name="Java">The Java the server runs on, which decides the platforms that can run it.</param>
/// <param name="MainClass">The class that the server's jar starts, which a test starts by name with the jar as its class path.</param>
/// <param name="Files">The server's files; the first is the jar that is its class path.</param>
internal sealed record LocalServer(int Java, string MainClass, IReadOnlyList<LocalServerFile> Files)
{
    private const string ForgeMaven = "https://maven.minecraftforge.net/";
    private const string Libraries = "https://libraries.minecraft.net/";

    /// <summary>
    /// Gets Forge 1.7.2-10.12.2.1161, which runs on Java 8, so everywhere but on Windows for ARM64. Forge's jar names the six libraries and the vanilla server in
    /// its manifest's class path by their paths beside it. Without the agent it ends with a <c>ConcurrentModificationException</c> as it sorts its tweakers.
    /// </summary>
    internal static LocalServer Forge172 { get; } = new(
        Java: 8,
        MainClass: "cpw.mods.fml.relauncher.ServerLaunchWrapper",
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
    /// and unpacks them into the directory it runs in.
    /// </summary>
    internal static LocalServer Vanilla1182 { get; } = new(
        Java: 17,
        MainClass: "net.minecraft.bundler.Main",
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
    /// on a free port of this machine only.
    /// </summary>
    /// <param name="directory">The empty directory to install into, which the server then runs in.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The path of the server's jar, its class path.</returns>
    internal async Task<string> InstallAsync(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);

        foreach (LocalServerFile file in Files)
        {
            string cached = await new PinnedArtifact(new Uri(file.Address), file.Sha256).GetPathAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            FileInfo installed = new(Path.Combine(directory.FullName, file.Path));

            installed.Directory?.Create();
            File.Copy(cached, installed.FullName);
        }

        string[] properties = ["server-ip=127.0.0.1", "server-port=" + FreePort().ToString(CultureInfo.InvariantCulture), "online-mode=false"];

        await File.WriteAllLinesAsync(Path.Combine(directory.FullName, path2: "server.properties"), properties, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        await File.WriteAllTextAsync(Path.Combine(directory.FullName, path2: "eula.txt"), contents: "eula=true\n", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return Path.Combine(directory.FullName, Files[index: 0].Path);
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
}
