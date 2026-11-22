using System.Net;

namespace ModLoaderFixes.Mappings;

/// <summary>
/// Downloads a file, and asks for it again when the connection breaks or the server is busy. Every job of a CI run builds the agent with an empty cache and so
/// downloads each mapping, about 17,000 files a run, and one connection that a server reset failed the build, and the run with it.
/// </summary>
/// <param name="client">The client that downloads.</param>
/// <param name="pause">The pause before the second attempt, which doubles before each further one.</param>
internal sealed class Downloader(HttpClient client, TimeSpan pause)
{
    /// <summary>How many times a file is asked for at most.</summary>
    internal const int Attempts = 5;

    /// <summary>Gets the pause before the second attempt of a build's downloads: 2 seconds, then 4, 8 and 16.</summary>
    internal static TimeSpan Pause { get; } = TimeSpan.FromSeconds(seconds: 2);

    /// <summary>
    /// Downloads a file. A connection that fails or breaks off, a request that times out and an answer of 408, 429 or 5xx are tried again; any other answer that
    /// isn't a success, such as 404, fails at once.
    /// </summary>
    /// <param name="address">The file's address.</param>
    /// <param name="cancellationToken">Cancels the download and the pauses.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="HttpRequestException">The server refused the file, or every attempt failed; the message names the address.</exception>
    internal async Task<byte[]> GetAsync(string address, CancellationToken cancellationToken)
    {
        Exception? failure = null;

        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(pause * (1 << (attempt - 2)), cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            try
            {
                return await GetOnceAsync(address, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null || IsTemporary(exception.StatusCode.Value))
            {
                failure = exception;
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The client's own time limit ran out.
                failure = exception;
            }
        }

        throw new HttpRequestException(message: $"{address} couldn't be downloaded in {Attempts} attempts: {failure?.Message}", failure);
    }

    private static bool IsTemporary(HttpStatusCode status)
    {
        return status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;
    }

    private async Task<byte[]> GetOnceAsync(string address, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(new Uri(address), cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return !response.IsSuccessStatusCode
            ? throw new HttpRequestException(message: $"{address} answered {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode)
            : await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }
}
