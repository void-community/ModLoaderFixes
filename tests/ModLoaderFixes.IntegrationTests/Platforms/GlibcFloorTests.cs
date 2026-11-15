using System.Text;
using System.Text.RegularExpressions;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Platforms;

/// <summary>The Linux agent that CI publishes, which must load on glibc 2.17: Forge from 1.17 to 1.20.4 runs on itzg's Ubuntu 20.04 image.</summary>
public sealed partial class GlibcFloorTests
{
    /// <summary>
    /// The agent asks for no glibc newer than 2.17. CI links it against the Ubuntu 18.04 root file system of .NET's own builds, while a publish on a developer's
    /// machine links against that machine's glibc, so the test runs in CI only.
    /// </summary>
    [Fact]
    public void PublishedAgentAsksForGlibc217AtMost()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable(variable: "CI") is "true",
            reason: "Only CI links the agent against an old glibc."
        );

        string agent = PublishedAgent.Find(name: "ModLoaderFixes").FilePath;
        // An ELF library names each glibc version it asks for in its string table, as GLIBC_2.17.
        string text = Encoding.Latin1.GetString(File.ReadAllBytes(agent));
        Version? newest = GlibcVersion().Matches(text).Max(static match => Version.Parse(match.Groups[groupnum: 1].Value));

        Assert.NotNull(newest);
        Assert.True(newest <= new Version(major: 2, minor: 17), $"{agent} asks for glibc {newest}, newer than 2.17.");
    }

    [GeneratedRegex(pattern: @"GLIBC_(\d+(?:\.\d+)+)")]
    private static partial Regex GlibcVersion();
}
