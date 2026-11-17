namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>One of the files a real server is made of, pinned by its checksum, with the place its installer would put it in the directory the server runs in.</summary>
/// <param name="Path">Where the file lies in the server's directory.</param>
/// <param name="Address">Where it is downloaded from.</param>
/// <param name="Sha256">Its checksum.</param>
internal sealed record LocalServerFile(string Path, string Address, string Sha256);
