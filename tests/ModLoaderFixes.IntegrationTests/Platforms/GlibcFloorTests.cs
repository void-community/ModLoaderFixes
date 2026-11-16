using System.Text;
using System.Text.RegularExpressions;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests.Platforms;

/// <summary>
/// The Linux agents that CI publishes, each of which must load on the oldest C library its platform is promised for. The linux-x64 and linux-arm64 agents must
/// load on glibc 2.17: Forge from 1.17 to 1.20.4 runs on itzg's Ubuntu 20.04 image. The linux-arm agent can't ask for less than glibc 2.34, the first with the
/// 64-bit time functions that .NET's own runtime for 32-bit ARM calls. The agents for Alpine use musl and must not ask for glibc at all.
/// </summary>
public sealed partial class GlibcFloorTests
{
    private const byte Class32 = 1;
    // The fifth byte of an ELF file tells its class, 1 for a 32-bit one.
    private const int ClassOffset = 4;

    /// <summary>
    /// The agent of the platform the tests run on asks for no newer glibc than that platform's floor, and for none when it is linked against musl. CI links the
    /// linux-x64 and linux-arm64 agents against the Ubuntu 18.04 root file system of .NET's own builds, while a publish on a developer's machine links against that
    /// machine's glibc, so the test runs in CI only.
    /// </summary>
    [Fact]
    public void PublishedAgentAsksForNoNewerGlibcThanItsPlatformsFloor()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable(variable: "CI") is "true",
            reason: "Only CI links the agent against an old glibc, and only a Linux agent names one."
        );

        string agent = PublishedAgent.Find(name: "ModLoaderFixes").FilePath;
        byte[] bytes = File.ReadAllBytes(agent);
        // An ELF library names the libraries it needs and each glibc version it asks for in its string table, as libc.so.6 and GLIBC_2.17.
        string text = Encoding.Latin1.GetString(bytes);
        Version? newest = GlibcVersion().Matches(text).Max(static match => Version.Parse(match.Groups[groupnum: 1].Value));

        if (text.Contains(value: "libc.musl-", StringComparison.Ordinal))
        {
            Assert.True(newest is null, $"{agent} is linked against musl and asks for glibc {newest}.");
            Assert.DoesNotContain(expectedSubstring: "libc.so.6", text, StringComparison.Ordinal);

            return;
        }

        Version floor = bytes[ClassOffset] == Class32 ? new Version(major: 2, minor: 34) : new Version(major: 2, minor: 17);

        Assert.NotNull(newest);
        Assert.True(newest <= floor, $"{agent} asks for glibc {newest}, newer than {floor}.");
    }

    [GeneratedRegex(pattern: @"GLIBC_(\d+(?:\.\d+)+)")]
    private static partial Regex GlibcVersion();
}
