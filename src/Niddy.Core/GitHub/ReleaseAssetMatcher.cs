using System.Runtime.InteropServices;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.GitHub;

/// <summary>
/// Picks the release asset built for an operating system and architecture by the words in the file names, e.g.
/// <c>app-1.2-linux-x64.tar.gz</c>, <c>app_Darwin_arm64.zip</c>, <c>app-x86_64-pc-windows-msvc.zip</c> or
/// <c>App-1.2.dmg</c>.
/// </summary>
internal static class ReleaseAssetMatcher
{
    private static readonly Dictionary<string, OS> OsWords = new(StringComparer.Ordinal)
    {
        ["windows"] = OS.Windows, ["win"] = OS.Windows, ["win32"] = OS.Windows, ["win64"] = OS.Windows,
        ["msvc"] = OS.Windows, ["mingw"] = OS.Windows, ["exe"] = OS.Windows, ["msi"] = OS.Windows,
        ["msix"] = OS.Windows,
        ["macos"] = OS.MacOS, ["osx"] = OS.MacOS, ["darwin"] = OS.MacOS, ["mac"] = OS.MacOS, ["apple"] = OS.MacOS,
        ["dmg"] = OS.MacOS, ["pkg"] = OS.MacOS,
        ["linux"] = OS.Linux, ["linux64"] = OS.Linux, ["appimage"] = OS.Linux, ["deb"] = OS.Linux,
        ["rpm"] = OS.Linux, ["flatpak"] = OS.Linux, ["snap"] = OS.Linux,
        ["freebsd"] = OS.FreeBSD,
        ["android"] = OS.Android, ["apk"] = OS.Android, ["aab"] = OS.Android,
        ["ios"] = OS.iOS, ["ipa"] = OS.iOS,
        ["tvos"] = OS.TvOS, ["watchos"] = OS.WatchOS,
        ["wasm"] = OS.Browser, ["browser"] = OS.Browser, ["wasi"] = OS.Wasi,
    };

    private static readonly Dictionary<string, Architecture> ArchitectureWords = new(StringComparer.Ordinal)
    {
        ["x64"] = Architecture.X64, ["amd64"] = Architecture.X64, ["win64"] = Architecture.X64,
        ["linux64"] = Architecture.X64, ["64bit"] = Architecture.X64,
        ["arm64"] = Architecture.Arm64, ["aarch64"] = Architecture.Arm64, ["armv8"] = Architecture.Arm64,
        ["x86"] = Architecture.X86, ["i386"] = Architecture.X86, ["i686"] = Architecture.X86,
        ["386"] = Architecture.X86, ["ia32"] = Architecture.X86, ["32bit"] = Architecture.X86,
        ["arm"] = Architecture.Arm, ["armv7"] = Architecture.Arm, ["armv7l"] = Architecture.Arm,
        ["armhf"] = Architecture.Arm, ["armv6"] = Architecture.Arm,
        ["riscv64"] = Architecture.RiscV64, ["s390x"] = Architecture.S390x, ["ppc64le"] = Architecture.Ppc64le,
        ["loongarch64"] = Architecture.LoongArch64,
    };

    private static readonly string[] IgnoredSuffixes =
        [".sha256", ".sha256sum", ".sha512", ".sha512sum", ".sha1", ".md5", ".asc", ".sig", ".minisig", ".pem",
            ".crt", ".sbom", ".spdx", ".intoto.jsonl", ".blockmap"];

    /// <summary>
    /// The best asset for <paramref name="os"/> and <paramref name="architecture"/>, or null. The name must mention the
    /// OS and no other; an exact architecture beats <c>universal</c>, which beats no architecture at all; glibc builds
    /// beat musl ones; ties go to the asset listed first.
    /// </summary>
    public static GitHubReleaseAsset? Find(IReadOnlyList<GitHubReleaseAsset> assets, OS os, Architecture architecture,
        Func<GitHubReleaseAsset, bool>? filter)
    {
        os = os switch
        {
            OS.iPadOS => OS.iOS,
            OS.MacCatalyst => OS.MacOS,
            _ => os,
        };

        GitHubReleaseAsset? best = null;
        int bestScore = int.MinValue;
        foreach (GitHubReleaseAsset asset in assets)
        {
            if (IsChecksumOrSignature(asset.Name) || (filter is not null && !filter(asset)))
            {
                continue;
            }

            int? score = Score(Words(asset.Name), os, architecture);
            if (score > bestScore)
            {
                best = asset;
                bestScore = score.Value;
            }
        }

        return best;
    }

    /// <summary>Whether a file is a checksum, signature or other file that rides along with the real assets.</summary>
    public static bool IsChecksumOrSignature(string name)
    {
        string lower = name.ToLowerInvariant();
        return IgnoredSuffixes.Any(lower.EndsWith) || lower.Contains("checksum") || lower.Contains("sha256sums");
    }

    private static int? Score(HashSet<string> words, OS os, Architecture architecture)
    {
        HashSet<OS> named = [.. words.Where(OsWords.ContainsKey).Select(word => OsWords[word])];
        // Android target triples say linux too (aarch64-linux-android), and iOS ones say apple.
        bool osMatches = named.Contains(os) && named.All(other => other == os
            || (os == OS.Android && other == OS.Linux)
            || (os == OS.iOS && other == OS.MacOS));
        if (!osMatches)
        {
            return null;
        }

        HashSet<Architecture> architectures = [.. words.Where(ArchitectureWords.ContainsKey).Select(word => ArchitectureWords[word])];
        bool universal = words.Contains("universal") || words.Contains("universal2");
        int score;
        if (architectures.Contains(architecture))
        {
            score = 2;
        }
        else if (universal && architecture is Architecture.X64 or Architecture.Arm64)
        {
            score = 1;
        }
        else if (architectures.Count == 0 && !universal)
        {
            score = 0;
        }
        else
        {
            return null;
        }

        return words.Contains("musl") ? score - 1 : score;
    }

    private static HashSet<string> Words(string name)
    {
        string lower = name.ToLowerInvariant()
            .Replace("x86_64", "x64", StringComparison.Ordinal)
            .Replace("x86-64", "x64", StringComparison.Ordinal)
            .Replace("64-bit", "64bit", StringComparison.Ordinal)
            .Replace("32-bit", "32bit", StringComparison.Ordinal);
        return [.. lower.Split(['-', '_', '.', ' ', '+', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)];
    }
}
