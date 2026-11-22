using System.IO.Compression;
using System.Net;
using System.Text;

using ModLoaderFixes.Mappings;

using Xunit;

namespace ModLoaderFixes.Tests.Mappings;

/// <summary>The generator's downloads, which ask again for a file when a connection breaks or a server is busy, as Forge's Maven was for three CI runs.</summary>
public sealed class DownloaderTests
{
    private const string Address = "https://example.invalid/mappings.zip";

    /// <summary>A connection that the server resets as the request is sent, as CI saw, costs an attempt, and the file comes with the next.</summary>
    [Fact]
    public async Task DownloadAsksAgainAfterAConnectionThatWasReset()
    {
        using ScriptedHandler handler = new(static (address, time) => time < 3 ? throw Reset() : Ok(content: "mappings"));

        byte[] bytes = await DownloadAsync(handler);

        Assert.Equal(expected: "mappings", Encoding.UTF8.GetString(bytes));
        Assert.Equal(expected: 3, handler.Requests[Address]);
    }

    /// <summary>A file whose content breaks off while it is read, the other failure CI saw, is asked for again from its start.</summary>
    [Fact]
    public async Task DownloadAsksAgainWhenTheContentBreaksOff()
    {
        using ScriptedHandler handler = new(static (address, time) => time == 1 ? Broken() : Ok(content: "whole"));

        byte[] bytes = await DownloadAsync(handler);

        Assert.Equal(expected: "whole", Encoding.UTF8.GetString(bytes));
        Assert.Equal(expected: 2, handler.Requests[Address]);
    }

    /// <summary>A server that says it is busy or broken, with 408, 429 or a 5xx answer, is asked again.</summary>
    [Fact]
    public async Task DownloadAsksAgainWhenTheServerIsBusy()
    {
        HttpStatusCode[] busy =
        [
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.ServiceUnavailable,
        ];

        foreach (HttpStatusCode status in busy)
            Assert.Equal(expected: 2, await RequestsUntilServedAsync(status));
    }

    /// <summary>A file that isn't there is a mistake in its address, which another attempt can't mend, so the download fails at once and says what was answered.</summary>
    [Fact]
    public async Task DownloadDoesNotAskAgainForAFileThatIsNotThere()
    {
        using ScriptedHandler handler = new(static (address, time) => new HttpResponseMessage(HttpStatusCode.NotFound));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() => DownloadAsync(handler));

        Assert.Equal(Address + " answered 404.", exception.Message);
        Assert.Equal(expected: 1, handler.Requests[Address]);
    }

    /// <summary>A server that resets every connection is asked five times, and the failure names the file and how the last attempt failed.</summary>
    [Fact]
    public async Task DownloadGivesUpAfterFiveAttemptsAndNamesTheAddress()
    {
        using ScriptedHandler handler = new(static (address, time) => throw Reset());

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() => DownloadAsync(handler));

        Assert.Equal(Address + " couldn't be downloaded in 5 attempts: An error occurred while sending the request.", exception.Message);
        Assert.Equal(expected: "Connection reset by peer.", Assert.IsType<HttpRequestException>(exception.InnerException).InnerException?.Message);
        Assert.Equal(expected: 5, handler.Requests[Address]);
    }

    /// <summary>
    /// The mapping sources ask again for each thing they download: Mojang's list of versions, Forge's two lists, Ornithe's list and a mapping's file. Here every
    /// address resets its first connection, and the sources still read Forge's mapping of the one release there is.
    /// </summary>
    [Fact]
    public async Task SourcesAskAgainForEveryListAndFile()
    {
        const string manifest = """{"versions":[{"id":"1.0","type":"release","url":"https://example.invalid/1.0.json","releaseTime":"2020-01-01T00:00:00+00:00"}]}""";
        const string forge = "https://maven.minecraftforge.net/de/oceanlabs/mcp/";

        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            [key: "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"] = Encoding.UTF8.GetBytes(manifest),
            [forge + "mcp/maven-metadata.xml"] = Encoding.UTF8
                .GetBytes(s: "<metadata><versioning><versions><version>1.0</version></versions></versioning></metadata>"),
            [forge + "mcp_config/maven-metadata.xml"] = Encoding.UTF8
                .GetBytes(s: "<metadata><versioning><versions /></versioning></metadata>"),
            [key: "https://meta.ornithemc.net/v3/versions/gen2/intermediary"] = Encoding.UTF8.GetBytes(s: "[]"),
            [forge + "mcp/1.0/mcp-1.0-srg.zip"] = Zip(entry: "joined.srg", text: "CL: a net/Manager\n"),
        };

        using ScriptedHandler handler = new((address, time) => time == 1
            ? throw Reset()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(files[address])
            });

        using HttpClient client = new(handler);

        DirectoryInfo cache = Directory.CreateTempSubdirectory(prefix: "modloaderfixes-downloads-");

        try
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            MappingSources sources = await MappingSources.CreateAsync(new Downloader(client, TimeSpan.Zero),
                cache.FullName,
                cancellationToken);

            Mapping mapping = await sources.GetAsync(Namespace.Srg, version: "1.0", cancellationToken);

            Assert.Equal(expected: "a", mapping.ObfuscatedClass(named: "net/Manager"));
            Assert.Equal(files.Keys.Order(StringComparer.Ordinal), handler.Requests.Keys.Order(StringComparer.Ordinal));
            Assert.All(handler.Requests.Values, static requests => Assert.Equal(expected: 2, requests));
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    // A response whose content gives a byte and then fails, as a connection does that is reset while a file is read.
    private static HttpResponseMessage Broken()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new BrokenStream())
        };
    }

    private static async Task<byte[]> DownloadAsync(ScriptedHandler handler)
    {
        using HttpClient client = new(handler, disposeHandler: false);

        return await new Downloader(client, TimeSpan.Zero).GetAsync(Address, TestContext.Current.CancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private static HttpResponseMessage Ok(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }

    // How often a file is asked for when the server first answers with the given status and then serves it.
    private static async Task<int> RequestsUntilServedAsync(HttpStatusCode first)
    {
        using ScriptedHandler handler = new((address, time) => time == 1 ? new HttpResponseMessage(first) : Ok(content: "later"));

        byte[] bytes = await DownloadAsync(handler).ConfigureAwait(continueOnCapturedContext: false);

        Assert.Equal(expected: "later", Encoding.UTF8.GetString(bytes));

        return handler.Requests[Address];
    }

    // What HttpClient throws when the server resets the connection.
    private static HttpRequestException Reset()
    {
        return new HttpRequestException(message: "An error occurred while sending the request.",
            new IOException(message: "Connection reset by peer."));
    }

    private static byte[] Zip(string entry, string text)
    {
        using MemoryStream stream = new();

        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using StreamWriter writer = new(archive.CreateEntry(entry).Open());

            writer.Write(text);
        }

        return stream.ToArray();
    }

    // Content that gives a byte and then fails.
    private sealed class BrokenStream : Stream
    {
        private bool _started;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_started)
                throw new IOException(message: "Connection reset by peer.");

            _started = true;
            buffer[offset] = (byte)'m';

            return 1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
