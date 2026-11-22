namespace ModLoaderFixes.Tests.Mappings;

/// <summary>A server written out in a test: it answers each request with what the test says for that address and for how often it has been asked for.</summary>
/// <param name="answer">Gives the answer to an address that is asked for the given time, counted from 1, or throws as a broken connection does.</param>
internal sealed class ScriptedHandler(Func<string, int, HttpResponseMessage> answer) : HttpMessageHandler
{
    private readonly Dictionary<string, int> _requests = new(StringComparer.Ordinal);

    /// <summary>Gets how often each address was asked for.</summary>
    internal IReadOnlyDictionary<string, int> Requests => _requests;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string address = request.RequestUri?.AbsoluteUri
            ?? throw new InvalidOperationException(message: "The request has no address.");

        int time = _requests.GetValueOrDefault(address) + 1;

        _requests[address] = time;

        return Task.FromResult(answer(address, time));
    }
}
