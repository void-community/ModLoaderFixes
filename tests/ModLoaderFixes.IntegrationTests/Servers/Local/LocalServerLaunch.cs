namespace ModLoaderFixes.IntegrationTests.Servers.Local;

/// <summary>How an installed server is started: the directory it runs in, the JVM's options before the class path, the class to start, the class path and what follows the class.</summary>
/// <param name="Directory">The server's directory, which it runs in.</param>
/// <param name="MainClass">The class to start.</param>
/// <param name="ClassPath">Where the JVM finds the class: the server's jar, or the server's directory when the class comes from a module path.</param>
/// <param name="JvmArguments">The JVM's options, such as a module path.</param>
/// <param name="Arguments">The program's arguments.</param>
internal sealed record LocalServerLaunch(string Directory, string MainClass, string ClassPath, IReadOnlyList<string> JvmArguments, IReadOnlyList<string> Arguments)
{
    // The JVM options of Forge's argument file that take their value as the next word.
    private static readonly string[] OptionsWithValue = ["-p", "--module-path", "--add-modules", "--add-exports", "--add-opens", "-cp", "--class-path"];

    /// <summary>
    /// Reads the file that Forge's installer writes for <c>java @file</c> from 1.17 on: the JVM's options, with the module path that holds the launcher, then the
    /// launcher's class and its arguments, separated by spaces. Forge names no class path there, so the server's directory stands in for one.
    /// </summary>
    /// <param name="text">The file's contents.</param>
    /// <param name="directory">The server's directory.</param>
    /// <returns>The launch.</returns>
    internal static LocalServerLaunch Parse(string text, string directory)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int main = 0;

        // The class is the first word that neither is an option nor the value of the option before it.
        while (main < words.Length && (words[main].StartsWith(value: '-') || (main > 0 && OptionsWithValue.Contains(words[main - 1]))))
            main++;

        return main == words.Length
            ? throw new FormatException("The argument file names no class to start: " + text)
            : new LocalServerLaunch(directory, words[main], directory, words[..main], words[(main + 1)..]);
    }
}
