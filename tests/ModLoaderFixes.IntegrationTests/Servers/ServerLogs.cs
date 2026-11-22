using DotNet.Testcontainers.Containers;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>
/// What a server in a container says about itself when a test has to show it: its log in the order it was written, and the threads of its JVM. A Forge server
/// with the agent twice answered itzg's health check for 13 minutes without finishing its start, and the test showed 29 lines of the health check and the agent's
/// lines after them, which were its standard error and not what it did last, so nothing told where it stood.
/// </summary>
internal static class ServerLogs
{
    /// <summary>The words of the line a Forge server from 1.18 logs for each connection that reaches it before it is ready, as itzg's health check does every few seconds.</summary>
    internal const string HealthCheck = "Server is still starting";

    // Docker starts each line with the time it was written, as 2026-11-16T02:22:00.313582511Z: 30 characters of a fixed width, which sort as the times do.
    private const int TimeLength = 30;

    /// <summary>
    /// Puts the lines of a container's two streams into the order they were written in. Docker hands each stream as a whole, so the standard error, where the
    /// agent logs, would follow the last line of the standard output, hours of it or none.
    /// </summary>
    /// <param name="standardOutput">The standard output, each line with Docker's time before it.</param>
    /// <param name="standardError">The standard error, likewise.</param>
    /// <returns>The lines of both by their times; lines of the same time keep their order, the standard output's first.</returns>
    internal static string Chronological(string standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        IEnumerable<string> lines = Lines(standardOutput).Concat(Lines(standardError));

        return string.Join(separator: '\n',
            lines.OrderBy(static line => line.Length < TimeLength ? line : line[..TimeLength], StringComparer.Ordinal));
    }

    /// <summary>
    /// Asks the JVM in the container for its threads and returns what it prints: HotSpot answers the QUIT signal with every thread's stack on its standard output
    /// and runs on. itzg's images hold a runtime without <c>jstack</c> or <c>jcmd</c>.
    /// </summary>
    /// <param name="server">The container.</param>
    /// <param name="cancellationToken">Stops the wait for the answer.</param>
    /// <returns>The threads, each line without Docker's time, or why there are none.</returns>
    internal static async Task<string> ThreadsAsync(IContainer server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.State != TestcontainersStates.Running)
            return "The container doesn't run, so its JVM has no threads to show.";

        string before = await ForgeServer.ReadLogsAsync(server, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        ExecResult signal = await server.ExecAsync(command: ["sh", "-c", "kill -3 $(pidof java)"], cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (signal.ExitCode != 0)
            return "No JVM in the container took the signal: " + signal.Stderr.Trim();

        // The JVM prints the stacks first and its JNI references last, a moment after the signal.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(milliseconds: 500), cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            string logs = await ForgeServer.ReadLogsAsync(server, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            int start = logs.IndexOf(value: "Full thread dump", Math.Min(before.Length, logs.Length), StringComparison.Ordinal);

            if (start >= 0 && logs.IndexOf(value: "JNI global ref", start, StringComparison.Ordinal) >= 0)
            {
                return string.Join(separator: '\n',
                    Lines(logs[start..]).Select(static (line, index) => index == 0 || line.Length <= TimeLength ? line : line[(TimeLength + 1)..]));
            }
        }

        return "The JVM took the signal and printed no threads within ten seconds.";
    }

    /// <summary>Leaves the health check's lines out of a log, which otherwise are all that the end of a log shows of a server that never became ready.</summary>
    /// <param name="logs">The log.</param>
    /// <returns>The log without them.</returns>
    internal static string WithoutHealthChecks(string logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return string.Join(separator: '\n',
            Lines(logs).Where(static line => !line.Contains(HealthCheck, StringComparison.Ordinal)));
    }

    private static string[] Lines(string text)
    {
        return text.Split(separator: '\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
