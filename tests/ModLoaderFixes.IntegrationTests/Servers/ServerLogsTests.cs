using Xunit;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>How the server tests show a server's log, which needs no server to check.</summary>
public sealed class ServerLogsTests
{
    /// <summary>The lines Forge logs for itzg's health check while it starts are left out of the log that a timeout shows, and the others stay in their order.</summary>
    [Fact]
    public void LogOfATimeoutLeavesTheHealthChecksLinesOut()
    {
        const string logs = "Preparing level\n[SERVERHOOKS]: Disconnecting VANILLA connection attempt: Server is still starting! Please wait before reconnecting.\n"
            + "Preparing start region\n[SERVERHOOKS]: Disconnecting Player (server is still starting): Server is still starting! Please wait before reconnecting.\n";

        Assert.Equal(expected: "Preparing level\nPreparing start region", ServerLogs.WithoutHealthChecks(logs));
    }

    /// <summary>
    /// The agent logs on the standard error and the server on the standard output, and Docker hands each as a whole. The log is read in the order of the times
    /// Docker gives each line, so that its end is what the server did last and not all the agent ever logged.
    /// </summary>
    [Fact]
    public void LogsOfBothStreamsAreReadInTheOrderTheyWereWritten()
    {
        const string output = "2026-11-16T02:21:59.000000001Z [init] Starting\n2026-11-16T02:22:05.000000000Z Preparing level\n2026-11-16T02:23:25.858168015Z Server is still starting!\n";
        const string error = "2026-11-16T02:22:00.313582511Z ModLoaderFixes: patched a\n2026-11-16T02:22:09.441914739Z ModLoaderFixes: patched b\n";

        string[] lines = ServerLogs.Chronological(output, error).Split(separator: '\n');

        Assert.Equal(
            ["[init] Starting", "ModLoaderFixes: patched a", "Preparing level", "ModLoaderFixes: patched b", "Server is still starting!"],
            lines.Select(static line => line[(line.IndexOf(value: ' ', StringComparison.Ordinal) + 1)..])
        );
    }
}
