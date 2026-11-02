using ModLoaderFixes.Mappings;

// The agent's build runs this before it compiles: it resolves the names of the MappedClass items it wrote out and writes Mappings.g.cs, caching what it downloads.
if (args is not [{ } requests, { } table, { } cache])
{
    await Console.Error.WriteLineAsync(value: "Usage: ModLoaderFixes.Mappings <requests> <Mappings.g.cs> <cache directory>").ConfigureAwait(continueOnCapturedContext: false);

    return 2;
}

using HttpClient client = new();

string text = await File.ReadAllTextAsync(requests).ConfigureAwait(continueOnCapturedContext: false);
string generated = await Generator.GenerateAsync(text, cache, client, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

await File.WriteAllTextAsync(table, generated).ConfigureAwait(continueOnCapturedContext: false);

return 0;
