using System.Runtime.InteropServices;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.Tests;

[Collection(PlatformStateCollection.Name)]
public sealed class PlatformTests
{
    [Fact]
    public void PlatformInfo_IsNotNullAndPopulated()
    {
        Platform info = Platform.Info;

        Assert.NotNull(info);
        Assert.NotEqual(OS.Unknown, info.OS);
        Assert.False(string.IsNullOrWhiteSpace(info.Triple));
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime));
        Assert.True(info.TotalMemoryBytes > 0);
        Assert.True(info.TotalGpuMemoryBytes > 0);
        Assert.True(info.TotalGpus >= 0);
        Assert.False(string.IsNullOrWhiteSpace(info.MachineName));
        Assert.False(string.IsNullOrWhiteSpace(info.CurrentDirectory));
        Assert.False(string.IsNullOrWhiteSpace(info.CommandLine));
        Assert.False(string.IsNullOrWhiteSpace(info.OperatingSystemVersion));
        Assert.Equal(Environment.OSVersion.Version, info.OSVersion);
        Assert.True(info.OSVersion.Major > 0);
        Assert.True(info.ProcessorCount > 0);
        Assert.Equal(info.Triple, info.ToString());
    }

    [Fact]
    public void OSEnum_ContainsExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(OS), OS.Windows));
        Assert.True(Enum.IsDefined(typeof(OS), OS.Linux));
        Assert.True(Enum.IsDefined(typeof(OS), OS.MacOS));
        Assert.True(Enum.IsDefined(typeof(OS), OS.FreeBSD));
        Assert.True(Enum.IsDefined(typeof(OS), OS.Android));
        Assert.True(Enum.IsDefined(typeof(OS), OS.iOS));
        Assert.True(Enum.IsDefined(typeof(OS), OS.iPadOS));
        Assert.True(Enum.IsDefined(typeof(OS), OS.WatchOS));
        Assert.True(Enum.IsDefined(typeof(OS), OS.TvOS));
        Assert.True(Enum.IsDefined(typeof(OS), OS.MacCatalyst));
        Assert.True(Enum.IsDefined(typeof(OS), OS.Browser));
        Assert.True(Enum.IsDefined(typeof(OS), OS.Wasi));
        Assert.True(Enum.IsDefined(typeof(OS), OS.Unknown));
    }

    [Theory]
    [InlineData(Architecture.X64, OS.Windows, false, "x86_64-pc-windows-msvc")]
    [InlineData(Architecture.Arm64, OS.Windows, false, "aarch64-pc-windows-msvc")]
    [InlineData(Architecture.X86, OS.Windows, false, "i686-pc-windows-msvc")]
    [InlineData(Architecture.Arm64, OS.MacOS, false, "aarch64-apple-darwin")]
    [InlineData(Architecture.Arm64, OS.MacCatalyst, false, "aarch64-apple-ios-macabi")]
    [InlineData(Architecture.Arm64, OS.iOS, false, "aarch64-apple-ios")]
    [InlineData(Architecture.Arm64, OS.iPadOS, false, "aarch64-apple-ipados")]
    [InlineData(Architecture.Arm64, OS.WatchOS, false, "aarch64-apple-watchos")]
    [InlineData(Architecture.Arm64, OS.TvOS, false, "aarch64-apple-tvos")]
    [InlineData(Architecture.X64, OS.Linux, false, "x86_64-unknown-linux-gnu")]
    [InlineData(Architecture.X64, OS.Linux, true, "x86_64-unknown-linux-musl")]
    [InlineData(Architecture.Arm64, OS.Linux, true, "aarch64-unknown-linux-musl")]
    [InlineData(Architecture.Arm, OS.Linux, false, "armv7-unknown-linux-gnueabihf")]
    [InlineData(Architecture.Arm, OS.Linux, true, "armv7-unknown-linux-musleabihf")]
    [InlineData(Architecture.Armv6, OS.Linux, false, "arm-unknown-linux-gnueabihf")]
    [InlineData(Architecture.RiscV64, OS.Linux, false, "riscv64gc-unknown-linux-gnu")]
    [InlineData(Architecture.LoongArch64, OS.Linux, false, "loongarch64-unknown-linux-gnu")]
    [InlineData(Architecture.S390x, OS.Linux, false, "s390x-unknown-linux-gnu")]
    [InlineData(Architecture.Ppc64le, OS.Linux, false, "powerpc64le-unknown-linux-gnu")]
    [InlineData(Architecture.Arm64, OS.Android, false, "aarch64-linux-android")]
    [InlineData(Architecture.Arm, OS.Android, false, "armv7-linux-androideabi")]
    [InlineData(Architecture.X64, OS.FreeBSD, false, "x86_64-unknown-freebsd")]
    [InlineData(Architecture.Wasm, OS.Browser, false, "wasm32-unknown-emscripten")]
    [InlineData(Architecture.Wasm, OS.Wasi, false, "wasm32-unknown-wasi")]
    [InlineData(Architecture.X64, OS.Unknown, false, "x86_64-unknown")]
    [InlineData((Architecture)999, OS.Linux, false, "unknown-unknown-linux-gnu")]
    public void BuildTriple_MatchesRustTargetNames(Architecture architecture, OS os, bool isMusl, string expected)
    {
        Assert.Equal(expected, Platform.BuildTriple(architecture, os, isMusl));
    }

    [Fact]
    public void BuildTriple_IgnoresMuslOutsideLinux()
    {
        Assert.Equal("aarch64-apple-darwin", Platform.BuildTriple(Architecture.Arm64, OS.MacOS, isMusl: true));
    }

    [Theory]
    [InlineData("iPad13,8", null, true)]
    [InlineData("iPhone15,2", null, false)]
    [InlineData("arm64", "iPad14,1", true)]     // simulator: hw.machine is the host CPU
    [InlineData("arm64", "iPhone16,1", false)]
    [InlineData("arm64", "", false)]
    [InlineData("", null, false)]
    public void IsIPadModel_UsesDeviceOrSimulatorModel(string machine, string? simulatorModel, bool expected)
    {
        Assert.Equal(expected, Platform.IsIPadModel(machine, simulatorModel));
    }

    // The tests below change Platform's static declaration. Every test class that reads Platform.Info (directly or
    // through SecureStorageFactory.Create, DebouncedFileWatcher or SettingsStore.Watch) is in PlatformStateCollection,
    // so they run one at a time.

    [Fact]
    public void DeclareSupportedOperatingSystems_CurrentOS_InfoSucceeds()
    {
        try
        {
            Platform.DeclareSupportedOperatingSystems(Platform.Info.OS, OS.Unknown);

            Assert.NotNull(Platform.Info);
            Assert.Equal([Platform.Info.OS, OS.Unknown], Platform.SupportedOperatingSystems!.Order());
        }
        finally
        {
            Platform.ClearSupportedOperatingSystems();
        }
    }

    [Fact]
    public void DeclareSupportedOperatingSystems_OtherOS_ThrowsNowAndOnEveryAccess()
    {
        OS current = Platform.Info.OS;
        OS other = current == OS.Wasi ? OS.Browser : OS.Wasi;
        try
        {
            // Info was already detected above, so the declaration itself checks.
            UnsupportedOperatingSystemException declared = Assert.Throws<UnsupportedOperatingSystemException>(
                () => Platform.DeclareSupportedOperatingSystems(other));
            Assert.Equal(current, declared.OperatingSystem);
            Assert.Equal([other], declared.SupportedOperatingSystems);
            Assert.Contains(other.ToString(), declared.Message, StringComparison.Ordinal);

            Assert.Throws<UnsupportedOperatingSystemException>(() => Platform.Info);
            Assert.Throws<UnsupportedOperatingSystemException>(() => Platform.Info);
        }
        finally
        {
            Platform.ClearSupportedOperatingSystems();
        }

        Assert.Null(Platform.SupportedOperatingSystems);
        Assert.Equal(current, Platform.Info.OS);
    }

    [Fact]
    public void DeclareSupportedOperatingSystems_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => Platform.DeclareSupportedOperatingSystems());
        Assert.Null(Platform.SupportedOperatingSystems);
    }

    [Fact]
    public void EnsureSupported_ChecksMembership()
    {
        Platform.EnsureSupported(OS.Linux, null);
        Platform.EnsureSupported(OS.Linux, new HashSet<OS> { OS.Linux, OS.Windows });

        UnsupportedOperatingSystemException ex = Assert.Throws<UnsupportedOperatingSystemException>(
            () => Platform.EnsureSupported(OS.Browser, new HashSet<OS> { OS.Windows, OS.MacOS }));
        Assert.Equal(OS.Browser, ex.OperatingSystem);
        Assert.IsAssignableFrom<PlatformNotSupportedException>(ex);
        Assert.Equal("Browser isn't supported. Supported: Windows, MacOS.", ex.Message);
    }

    [Fact]
    public void Platform_MatchesCurrentRuntimeInformation()
    {
        Platform info = Platform.Info;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(OS.Windows, info.OS);
            Assert.Contains("windows", info.Triple, StringComparison.Ordinal);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.True(info.OS is OS.MacOS or OS.MacCatalyst or OS.iOS or OS.iPadOS or OS.WatchOS or OS.TvOS);
            Assert.Contains("apple", info.Triple, StringComparison.Ordinal);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.True(info.OS is OS.Linux or OS.Android);
        }
    }
}
