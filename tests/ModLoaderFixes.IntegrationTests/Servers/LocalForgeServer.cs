using System.Globalization;
using System.Net;
using System.Net.Sockets;

using JvmBridge.Testing;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// A real Forge 1.7.2 server laid out in a directory, to run as a process of the platform the tests run on, with that platform's own JDK and agent. The servers
/// in itzg's container are 64-bit Linux with glibc on every machine, so only this one meets the library of Windows, macOS, Alpine and 32-bit ARM. The files are
/// what Forge's installer puts there, each pinned by its checksum: the installer itself asks a mirror it picks from a list, once, and fails when a connection does.
/// </summary>
internal static class LocalForgeServer
{
    /// <summary>The class that the server's jar starts, which a test starts by name with the jar as its class path.</summary>
    internal const string MainClass = "cpw.mods.fml.relauncher.ServerLaunchWrapper";

    private const string Forge = "https://maven.minecraftforge.net/";
    private const string Libraries = "https://libraries.minecraft.net/";
    private const string Universal = "forge-1.7.2-10.12.2.1161-mc172-universal.jar";

    // Forge's jar names the libraries and the vanilla server in its manifest's class path by these paths, relative to itself.
    private static readonly ServerFile[] Files =
    [
        new(
            Universal,
            Forge + "net/minecraftforge/forge/1.7.2-10.12.2.1161-mc172/" + Universal,
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
            Forge,
            path: "org/scala-lang/scala-library/2.10.2/scala-library-2.10.2.jar",
            sha256: "9b49ec36ccdea13fbca8d1e1e2a74a8cc94e4fa0a9aced1fc74f8df32c1b1b99"
        ),
        Library(
            Forge,
            path: "org/scala-lang/scala-compiler/2.10.2/scala-compiler-2.10.2.jar",
            sha256: "019ae7f9bb54f08d7d8057fb6380c4bf8faaa878e501567913dcb3a3c0f95d9f"
        ),
        Library(
            Libraries,
            path: "net/sf/jopt-simple/jopt-simple/4.5/jopt-simple-4.5.jar",
            sha256: "64335a48be6b142a7d0c13ec5c82a707857aa58c0234e6eda3eaca0e96eea51b"
        ),
        Library(Libraries, path: "lzma/lzma/0.0.1/lzma-0.0.1.jar", sha256: "0c3085cb93c3ef95d731c274b6f2dd195aa0405fe9328d69cab7f5e899d5e124"),
    ];

    /// <summary>
    /// Puts the server's files into a directory, with properties that have it listen on a free port of this machine only and generate a flat world, which takes
    /// the least time.
    /// </summary>
    /// <param name="directory">The empty directory to install into, which the server then runs in.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>The path of Forge's jar, the server's class path.</returns>
    internal static async Task<string> InstallAsync(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        foreach (ServerFile file in Files)
        {
            string cached = await new PinnedArtifact(new Uri(file.Address), file.Sha256).GetPathAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            FileInfo installed = new(Path.Combine(directory.FullName, file.Path));

            installed.Directory?.Create();
            File.Copy(cached, installed.FullName);
        }

        string[] properties = ["server-ip=127.0.0.1", "server-port=" + FreePort().ToString(CultureInfo.InvariantCulture), "online-mode=false", "level-type=FLAT"];

        await File.WriteAllLinesAsync(Path.Combine(directory.FullName, path2: "server.properties"), properties, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return Path.Combine(directory.FullName, Universal);
    }

    // A port that was free a moment ago: the system picks one for a listener that is closed again at once.
    private static int FreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, port: 0);

        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static ServerFile Library(string repository, string path, string sha256)
    {
        return new ServerFile("libraries/" + path, repository + path, sha256);
    }

    // A file of the server: where it lies in the server's directory, where it is downloaded from and its checksum.
    private sealed record ServerFile(string Path, string Address, string Sha256);
}
