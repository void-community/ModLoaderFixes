using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>Starts Forge servers as itzg installs them, with the agent loaded, and logs players in to them.</summary>
internal static partial class ForgeServer
{
    /// <summary>
    /// The line the agent logs when it makes the synchronous chunk load of Forge 1.7.2 to 1.9 return the chunk that its provider has, also when Forge
    /// generated it because the read found nothing.
    /// </summary>
    internal const string ChunkLoadPatched =
        "ModLoaderFixes: patched net/minecraftforge/common/chunkio/ChunkIOExecutor (synchronous chunk loads return the chunk the provider has (1 change))";

    /// <summary>
    /// The line the agent logs when it points the close of a connection's channel at the helper that sends what is unsent first, under FML's names, in Forge for
    /// Minecraft 1.7.2 to 1.8.9.
    /// </summary>
    internal const string ClosePatched =
        "ModLoaderFixes: patched net/minecraft/network/NetworkManager (func_150718_a(Lnet/minecraft/util/IChatComponent;)V: 1 invocation of io/netty/channel/Channel.close redirected)";

    /// <summary>
    /// The xUnit collection of the server tests, which xUnit runs one at a time: each installs its server from Mojang's and Forge's servers, and installs that run side by
    /// side fail their downloads more often.
    /// </summary>
    internal const string Collection = "Forge servers";

    /// <summary>
    /// The line the agent logs when it lets one thread at a time flush NetworkManager's outbound queue, under FML's names, in Forge for Minecraft 1.7.2 to 1.14.3.
    /// Forge ships NetworkManager as a binary patch of Minecraft's, so only a running Forge shows it.
    /// </summary>
    internal const string FlushPatched = "ModLoaderFixes: patched net/minecraft/network/NetworkManager (outbound queue flushed by one thread at a time (1 change))";

    /// <summary>
    /// The end of the line the agent logs when it makes the region that Minecraft 1.13 to 1.16.5 generates through remove the block entity of a block that
    /// another block replaces, after the region's name: MCP's under Forge, CraftBukkit's under Paper and an obfuscated one in vanilla.
    /// </summary>
    internal const string GenerationPatched = " (block entity of another block removed where generation replaces the block (1 change))";

    /// <summary>The line the agent logs when it makes the class hierarchy cache of ModLauncher 2 to 5, which Forge for Minecraft 1.13.2 to 1.15.2 ships, safe to share.</summary>
    internal const string ModLauncherPatched =
        "ModLoaderFixes: patched cpw/mods/modlauncher/TransformerClassWriter (<clinit>()V: 1 new java/util/HashMap() replaced by modloaderfixes/ClassHierarchyCache.parents() stored in classParents";

    /// <summary>
    /// What the agent logs, after the container's name, when it has the container of a chunk section's blocks, in Minecraft from 1.18, read a block again
    /// while the block's palette entry is on its way.
    /// </summary>
    internal const string PaletteReadsPatched = " (blocks read again while their palette entry is on its way (1 change))";

    /// <summary>The line a server logs when it is ready for players.</summary>
    internal const string ReadyLine = "Done (";

    /// <summary>
    /// The line the agent logs when it makes the map of union file systems of securejarhandler's provider a concurrent one, in Forge from 1.17.1 and in
    /// NeoForge up to 21.8, which read their jars through it.
    /// </summary>
    internal const string UnionFileSystemsPatched =
        "ModLoaderFixes: patched cpw/mods/niofs/union/UnionFileSystemProvider (<init>()V: 1 new java/util/HashMap replaced by new java/util/concurrent/ConcurrentHashMap stored in fileSystems)";

    /// <summary>
    /// The end of the line the agent logs when it points the JDK's zip file system at the helper that keeps its channel readable after an interrupt, in Java 8's
    /// com/sun/nio/zipfs and in later releases' jdk/nio/zipfs.
    /// </summary>
    internal const string ZipChannelPatched = "ZipFileSystem (<init>: 1 invocation of java/nio/file/Files.newByteChannel redirected)";

    private const string AgentDirectory = "/patcher/";
    private const int InstallAttempts = 3;
    private const string Kicks = "40";
    private const string LoaderDirectory = "/loader/";

    // The images this run has pulled already.
    private static readonly ConcurrentDictionary<string, bool> Refreshed = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the runtime whose library a server container loads: 64-bit Linux with glibc of the machine's architecture, as itzg's images are, whatever platform the
    /// tests run on. Tests in an Alpine container, or in a 32-bit ARM one on an ARM64 machine, start the same server containers beside their own.
    /// </summary>
    internal static string Runtime => RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64 ? "linux-arm64" : "linux-x64";

    /// <summary>Gets how long installing and starting a server may take.</summary>
    internal static TimeSpan StartTimeout { get; } = TimeSpan.FromMinutes(minutes: 15);

    /// <summary>
    /// Checks that every player the kick client had the server kick got the reason and then the end of its connection: none lost the reason, none saw its
    /// connection reset, which is how a server that closes with the player's packets unread ends it without the agent, and none had its connection ended before
    /// it said anything. When one did, the failure shows what the server logged about each such player, which the kick client names.
    /// </summary>
    /// <param name="kicks">The kick client's result, from <see cref="KickPlayersAsync"/>.</param>
    /// <param name="logs">The server's log after the kicks.</param>
    internal static void AssertKicked(ExecResult kicks, string logs)
    {
        bool isReceived = kicks.ExitCode is 0 && kicks.Stdout.Contains("KICKS {received=" + Kicks + "}", StringComparison.Ordinal);

        Assert.True(
            isReceived,
            kicks.Stdout + kicks.Stderr + Environment.NewLine + "The server's log about them:" + Environment.NewLine + About(logs, kicks.Stdout)
        );
    }

    /// <summary>Checks that the server's log has a line, and shows the end of the log when it hasn't: the start, which is all a failed Contains prints, is itzg's setup.</summary>
    /// <param name="logs">The server's log.</param>
    /// <param name="expected">The text a line must have.</param>
    internal static void AssertLogged(string logs, string expected)
    {
        Assert.True(
            logs.Contains(expected, StringComparison.Ordinal),
            $"The server didn't log \"{expected}\". Its log ends with:{Environment.NewLine}{Tail(logs)}"
        );
    }

    /// <summary>Checks that no line of the server's log has a text, and shows the lines that have it when one does.</summary>
    /// <param name="logs">The server's log.</param>
    /// <param name="unexpected">The text no line may have.</param>
    internal static void AssertNotLogged(string logs, string unexpected)
    {
        string[] lines = [.. logs.Split(separator: '\n').Where(line => line.Contains(unexpected, StringComparison.Ordinal))];

        Assert.True(
            lines.Length == 0,
            $"The server logged \"{unexpected}\":{Environment.NewLine}{string.Join(Environment.NewLine, lines.Take(count: 10))}"
        );
    }

    /// <summary>Compiles the login client and the kick client for Java 8, which every server image runs.</summary>
    /// <param name="cancellationToken">Stops the compilation.</param>
    /// <returns>The directory of its classes.</returns>
    internal static Task<string> CompileLoginLoadAsync(CancellationToken cancellationToken)
    {
        return new Jdk(major: 8).CompileAsync(sources: "Fixtures/LoginLoad/**/*.java", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Describes a server of the given loader and version that loads the agent with <paramref name="option"/>. The test is skipped where the host isn't Linux: the
    /// servers run in Linux containers, which load the agent of a Linux platform.
    /// </summary>
    /// <param name="image">The itzg/minecraft-server image, which decides the Java version.</param>
    /// <param name="type">itzg's TYPE: FORGE or NEOFORGE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="memory">The server's heap.</param>
    /// <param name="option">The JVM option that loads the agent: <c>-agentpath:</c> or <c>-javaagent:</c>.</param>
    /// <param name="agent">The agent's library or jar.</param>
    /// <returns>The container's builder.</returns>
    internal static ContainerBuilder Create(string image, string type, string version, string memory, string option, FileInfo agent)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), reason: "The servers run in Linux containers, which load the agent of a Linux platform.");

        // The images are named by itzg's tags of its latest release, which move. Docker keeps the image it pulled first, so each run pulls an image when it first
        // uses it, which costs little when it is the one Docker has, and takes what Docker has from then on.
        return new ContainerBuilder(image)
            .WithImagePullPolicy(cached => Refreshed.TryAdd(image, value: true) || cached is null)
            .WithEnvironment(name: "EULA", value: "TRUE")
            .WithEnvironment(name: "TYPE", type)
            .WithEnvironment(name: "VERSION", version)
            .WithEnvironment(name: "MEMORY", memory)
            .WithEnvironment(name: "JVM_OPTS", option + AgentDirectory + agent.Name)
            .WithResourceMapping(FilePath.Of(agent.FullName), DirectoryPath.Of(AgentDirectory));
    }

    /// <summary>
    /// Has the server kick 40 players, one after another, from inside its container: each logs in, as a Forge client when the server is Forge 1.7, and sends several
    /// thousand Player packets a second, so the server often has one unread when it closes the connection. The player then says something and, right after it,
    /// something with a character that chat doesn't allow, for which the server kicks it as an operator's kick would. The server's remote console isn't used: on a
    /// busy machine a server now and then starts it without it ever accepting a connection.
    /// </summary>
    /// <param name="server">The started server, built with <see cref="WithLoginLoad"/>.</param>
    /// <param name="protocol">The protocol version of the server's Minecraft, 1.7's or 1.8's.</param>
    /// <param name="cancellationToken">Stops the kicks.</param>
    /// <returns>The kick client's result; it prints <c>KICKS {received=40}</c> and exits with 0 when every player got the reason and then the end of its connection.</returns>
    internal static async Task<ExecResult> KickPlayersAsync(IContainer server, int protocol, CancellationToken cancellationToken)
    {
        List<string> command = ["java", "-cp", LoaderDirectory, "loginload.KickLoad", "127.0.0.1", "25565", protocol.ToString(CultureInfo.InvariantCulture), Kicks];

        try
        {
            return await server.ExecAsync(command, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw await TimedOutAsync(server, phase: "The kicks didn't finish", exception).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>
    /// Logs 200 players in, 40 at a time, from inside the server's container, so the logins load its event loops without depending on the host's port forwarding.
    /// Forge logs its ready line before it admits players, so one player logs in first, until the server lets it, and the 200 follow.
    /// </summary>
    /// <param name="server">The started server.</param>
    /// <param name="protocol">The protocol version of the server's Minecraft.</param>
    /// <param name="fmlVersion">FML's network version, to log in as a Forge client, or <see langword="null"/> to log in as a vanilla one.</param>
    /// <param name="cancellationToken">Stops the logins.</param>
    /// <returns>The login client's result; it prints <c>LOGINS {logged in=200}</c> and exits with 0 when every login reached the play state.</returns>
    internal static async Task<ExecResult> LogInPlayersAsync(IContainer server, int protocol, string? fmlVersion, CancellationToken cancellationToken)
    {
        List<string> command = ["java", "-cp", LoaderDirectory, "loginload.LoginLoad", "127.0.0.1", "25565", protocol.ToString(CultureInfo.InvariantCulture), "200", "40"];

        if (fmlVersion is not null)
            command.Add(fmlVersion);

        try
        {
            return await server.ExecAsync(command, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw await TimedOutAsync(server, phase: "The logins didn't finish", exception).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Removes terminal escape sequences, then every other control character but line breaks and tabs.</summary>
    /// <param name="text">The text as a terminal would show it.</param>
    /// <returns>The text without them.</returns>
    internal static string Printable(string text)
    {
        return ControlCharacter().Replace(EscapeSequence().Replace(text, string.Empty), string.Empty);
    }

    /// <summary>
    /// Reads the server's log, both streams in the order their lines were written, without the terminal's colors and other control characters. Forge colors its log, and xUnit drops the result of a test
    /// whose failure message has an escape character, without counting it as failed, so a server that exits would pass unseen once its log is in the message.
    /// </summary>
    /// <param name="server">The container whose output is the server's log.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The log.</returns>
    internal static async Task<string> ReadLogsAsync(IContainer server, CancellationToken cancellationToken)
    {
        (string standardOutput, string standardError) = await server.GetLogsAsync(ct: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return Printable(ServerLogs.Chronological(standardOutput, standardError));
    }

    /// <summary>
    /// Starts the server and waits until it is ready or has exited. itzg installs the loader first, and the installer downloads from Mojang's and Forge's servers; when
    /// a download fails the install does too, and the container is started again, up to <see cref="InstallAttempts"/> times, as itzg installs again on a new start.
    /// </summary>
    /// <param name="server">The container that installs and runs the server.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The server's log so far, across its starts.</returns>
    internal static async Task<string> StartAsync(IContainer server, CancellationToken cancellationToken)
    {
        try
        {
            return await StartUntilReadyAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw await TimedOutAsync(server, phase: "The server wasn't ready", exception).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Lets players in without accounts, as many as a burst of logins brings, and puts the login client and the kick client beside the server.</summary>
    /// <param name="server">The server's builder.</param>
    /// <param name="loginLoad">The compiled clients, from <see cref="CompileLoginLoadAsync"/>.</param>
    /// <returns>The builder.</returns>
    internal static ContainerBuilder WithLoginLoad(this ContainerBuilder server, string loginLoad)
    {
        return server
            .WithEnvironment(name: "ONLINE_MODE", value: "FALSE")
            .WithEnvironment(name: "MAX_PLAYERS", value: "1000")
            .WithResourceMapping(DirectoryPath.Of(loginLoad), DirectoryPath.Of(LoaderDirectory));
    }

    // The server's lines from the first to a few after the last that names a player whose kick wasn't received, or its last lines when the kick client named
    // none: a player's own lines say how the server ended its connection, and those between and after them hold what the server logged without its name.
    private static string About(string logs, string kicks)
    {
        string[] lines = logs.Split(separator: '\n');
        string[] players = [.. UnreceivedPlayer().Matches(kicks).Select(static match => match.Groups[groupnum: 1].Value)];
        int[] named = [.. Enumerable.Range(start: 0, lines.Length).Where(index => players.Any(player => PlayerName(player).IsMatch(lines[index])))];

        return named.Length == 0 ? Tail(logs) : string.Join(separator: '\n', lines.Skip(named[0]).Take(Math.Min(named[^1] - named[0] + 20, val2: 200)));
    }

    [GeneratedRegex(pattern: @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlCharacter();

    // itzg logs "[init] [ERROR] Failed to install Forge" (or NeoForge) once per failed install.
    private static int CountInstallFailures(string logs)
    {
        return logs.Split(separator: '\n').Count(static line => line.Contains(value: "[ERROR] Failed to install", StringComparison.Ordinal));
    }

    [GeneratedRegex(pattern: @"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex EscapeSequence();

    // A player's name as a word of its own, so that kick1 isn't found in kick10.
    private static Regex PlayerName(string player)
    {
        return new Regex(@"\b" + Regex.Escape(player) + @"\b", RegexOptions.CultureInvariant);
    }

    private static async Task<string> StartUntilReadyAsync(IContainer server, CancellationToken cancellationToken)
    {
        string logs = string.Empty;

        for (int attempt = 1; attempt <= InstallAttempts; attempt++)
        {
            await server.StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Task<long> exit = server.GetExitCodeAsync(cancellationToken);

            logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            while (!logs.Contains(ReadyLine, StringComparison.Ordinal) && !exit.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds: 2), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (logs.Contains(ReadyLine, StringComparison.Ordinal) || CountInstallFailures(logs) < attempt)
                return logs;
        }

        return logs;
    }

    private static string Tail(string logs)
    {
        string[] lines = logs.Split(separator: '\n');

        return string.Join(separator: '\n', lines.Skip(Math.Max(val1: 0, lines.Length - 40)));
    }

    // A timed-out wait would otherwise report only that a task was canceled. The end of the log, without the health check's lines, says whether the install, the
    // start or the logins took the time, and the JVM's threads say what each of them waited for.
    private static async Task<TimeoutException> TimedOutAsync(IContainer server, string phase, OperationCanceledException exception)
    {
        string logs = await ReadLogsAsync(server, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
        string threads = await ServerLogs.ThreadsAsync(server, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        string message = string.Join(
            Environment.NewLine,
            value: [$"{phase} within {StartTimeout.TotalMinutes} minutes. The server's log, without the health check's lines, ends with:", Tail(ServerLogs.WithoutHealthChecks(logs)), "The threads of its JVM:", threads]
        );

        return new TimeoutException(message, exception);
    }

    [GeneratedRegex(pattern: @"^NOT RECEIVED by (\S+):", RegexOptions.Multiline)]
    private static partial Regex UnreceivedPlayer();
}
