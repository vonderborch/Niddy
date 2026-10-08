using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace Niddy.Core.SystemInfo;

/// <summary>
/// Provides information about the platform, operating system, hardware, and environment.
/// </summary>
public record Platform
{
    private const ulong DefaultGpuBytes = 4L * 1024 * 1024 * 1024; // Default to 4 GB
    
    private static Platform? _info = null;

    private static IReadOnlySet<OS>? _supportedOperatingSystems;

    /// <summary>
    /// Gets the cached platform information detected on the current machine.
    /// </summary>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// <see cref="DeclareSupportedOperatingSystems"/> was called and the detected OS isn't one of those declared.
    /// Thrown on every access, not just the first.
    /// </exception>
    public static Platform Info
    {
        get
        {
            Platform info = _info ??= Detect();
            EnsureSupported(info.OS, _supportedOperatingSystems);
            return info;
        }
    }

    /// <summary>
    /// Gets the operating systems the app declared it supports, or null if it hasn't declared any (all are allowed).
    /// </summary>
    public static IReadOnlySet<OS>? SupportedOperatingSystems => _supportedOperatingSystems;

    /// <summary>
    /// Declares the operating systems the app supports. From then on, <see cref="Info"/> throws
    /// <see cref="UnsupportedOperatingSystemException"/> when the app runs anywhere else. Call it once, early in
    /// startup. A later call replaces the earlier declaration.
    /// </summary>
    /// <param name="supported">The supported operating systems. At least one.</param>
    /// <exception cref="ArgumentException"><paramref name="supported"/> is empty.</exception>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// The platform has already been detected and its OS isn't in <paramref name="supported"/>.
    /// </exception>
    public static void DeclareSupportedOperatingSystems(params IEnumerable<OS> supported)
    {
        ArgumentNullException.ThrowIfNull(supported);
        FrozenSet<OS> set = supported.ToFrozenSet();
        if (set.Count == 0)
        {
            throw new ArgumentException("Declare at least one supported operating system.", nameof(supported));
        }

        _supportedOperatingSystems = set;
        if (_info is not null)
        {
            EnsureSupported(_info.OS, set);
        }
    }

    /// <summary>Removes the declaration, so every operating system is allowed again.</summary>
    internal static void ClearSupportedOperatingSystems()
    {
        _supportedOperatingSystems = null;
    }

    internal static void EnsureSupported(OS os, IReadOnlySet<OS>? supported)
    {
        if (supported is not null && !supported.Contains(os))
        {
            throw new UnsupportedOperatingSystemException(os, supported);
        }
    }

    // Order matters: Android also reports Linux, and Mac Catalyst/watchOS/tvOS/iPadOS also report iOS.
    private static readonly (Func<bool> IsMatch, OS OS)[] OsDetectors =
    [
        (OperatingSystem.IsBrowser, OS.Browser),
        (OperatingSystem.IsWasi, OS.Wasi),
        (OperatingSystem.IsAndroid, OS.Android),
        (OperatingSystem.IsMacCatalyst, OS.MacCatalyst),
        (OperatingSystem.IsWatchOS, OS.WatchOS),
        (OperatingSystem.IsTvOS, OS.TvOS),
        (IsIPadOS, OS.iPadOS),
        (OperatingSystem.IsIOS, OS.iOS),
        (OperatingSystem.IsWindows, OS.Windows),
        (OperatingSystem.IsMacOS, OS.MacOS),
        (OperatingSystem.IsLinux, OS.Linux),
        (OperatingSystem.IsFreeBSD, OS.FreeBSD),
    ];

    private static Platform Detect()
    {
        Architecture architecture = RuntimeInformation.ProcessArchitecture;

        // Determine OS: first matching detector wins
        OS os = OS.Unknown;
        foreach ((Func<bool> isMatch, OS candidateOs) in OsDetectors)
        {
            if (isMatch())
            {
                os = candidateOs;
                break;
            }
        }

        // Get GPUs, SysMem, GpuMem
        int gpus = EstimateNumberOfGpus();
        ulong sysMem = GetTotalSystemMemory();
        ulong gpuMem = GetTotalGpuMemory(sysMem);

        string triple = BuildTriple(architecture, os, os == OS.Linux && IsMusl());
        return new Platform(os, Environment.OSVersion.Version, architecture, RuntimeInformation.FrameworkDescription, triple,
            gpus, sysMem, gpuMem);
    }

    /// <summary>
    /// Builds a Rust/LLVM-style target triple, e.g. <c>aarch64-apple-darwin</c> or <c>armv7-unknown-linux-musleabihf</c>.
    /// </summary>
    internal static string BuildTriple(Architecture architecture, OS os, bool isMusl)
    {
        bool isArm32 = architecture is Architecture.Arm or Architecture.Armv6;
        string arch = architecture switch
        {
            Architecture.X86 => "i686",
            Architecture.X64 => "x86_64",
            Architecture.Arm => "armv7",
            Architecture.Armv6 => "arm",
            Architecture.Arm64 => "aarch64",
            Architecture.Wasm => "wasm32",
            Architecture.S390x => "s390x",
            Architecture.LoongArch64 => "loongarch64",
            Architecture.Ppc64le => "powerpc64le",
            Architecture.RiscV64 => "riscv64gc",
            _ => "unknown"
        };
        string platform = os switch
        {
            OS.Windows => "pc-windows-msvc",
            OS.MacOS => "apple-darwin",
            OS.MacCatalyst => "apple-ios-macabi",
            OS.iOS => "apple-ios",
            OS.iPadOS => "apple-ipados",
            OS.WatchOS => "apple-watchos",
            OS.TvOS => "apple-tvos",
            OS.Android => isArm32 ? "linux-androideabi" : "linux-android",
            OS.Linux => $"unknown-linux-{(isMusl ? "musl" : "gnu")}{(isArm32 ? "eabihf" : "")}",
            OS.FreeBSD => "unknown-freebsd",
            OS.Browser => "unknown-emscripten",
            OS.Wasi => "unknown-wasi",
            _ => "unknown"
        };
        return $"{arch}-{platform}";
    }

    /// <summary>
    /// Determines whether the runtime was built against musl libc (Alpine and other musl distributions).
    /// </summary>
    private static bool IsMusl()
    {
        return RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase);
    }

    private Platform(OS os, Version osVersion, Architecture architecture, string runtime, string triple, int totalGpus,
        ulong memoryBytes, ulong gpuMemoryBytes)
    {
        OS = os;
        OSVersion = osVersion;
        Architecture = architecture;
        Runtime = runtime;
        Triple = triple;
        TotalGpus = totalGpus;
        TotalMemoryBytes = memoryBytes;
        TotalGpuMemoryBytes = gpuMemoryBytes;
    }

    /// <summary>
    /// Gets the detected operating system.
    /// </summary>
    public OS OS { get; }

    /// <summary>
    /// Gets the version of the detected operating system, from <see cref="Environment.OSVersion"/>.
    /// </summary>
    /// <remarks>
    /// On Windows this is the NT version (10.0.22631 on Windows 11), on macOS and iOS the product version (15.0),
    /// and on Linux and Android the kernel version (6.8.0), not the distribution or Android release. In a browser
    /// it doesn't identify the browser or the host OS.
    /// </remarks>
    public Version OSVersion { get; }

    /// <summary>
    /// Gets the process architecture. Any value .NET reports is kept, including ones Niddy has no triple name for.
    /// </summary>
    public Architecture Architecture { get; }
    
    /// <summary>
    /// Gets the .NET runtime framework description.
    /// </summary>
    public string Runtime { get; }
    
    /// <summary>
    /// Gets the Rust/LLVM-style target triple, e.g. <c>aarch64-apple-darwin</c>, <c>x86_64-pc-windows-msvc</c> or
    /// <c>x86_64-unknown-linux-musl</c>.
    /// </summary>
    public string Triple { get; }

    /// <summary>
    /// Gets the estimated number of GPUs available.
    /// </summary>
    public int TotalGpus { get; }
    
    /// <summary>
    /// Gets the total system physical memory in bytes.
    /// </summary>
    public ulong TotalMemoryBytes { get; }
    
    /// <summary>
    /// Gets the estimated total GPU video/graphics memory in bytes.
    /// </summary>
    public ulong TotalGpuMemoryBytes { get; }

    /// <summary>
    /// Gets the command line used to launch the current process.
    /// </summary>
    public string CommandLine => Environment.CommandLine;
    
    /// <summary>
    /// Gets the current working directory.
    /// </summary>
    public string CurrentDirectory => Directory.GetCurrentDirectory();
    
    /// <summary>
    /// Gets the machine name.
    /// </summary>
    public string MachineName => Environment.MachineName;
    
    /// <summary>
    /// Gets the operating system version string.
    /// </summary>
    public string OperatingSystemVersion => Environment.OSVersion.VersionString;
    
    /// <summary>
    /// Gets the number of logical processors.
    /// </summary>
    public int ProcessorCount => Environment.ProcessorCount;
    
    /// <summary>
    /// Gets the system directory path.
    /// </summary>
    public string SystemDirectory => Environment.SystemDirectory;
    
    /// <summary>
    /// Gets the user's documents directory path.
    /// </summary>
    public string UserDocumentsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    
    /// <summary>
    /// Gets the user's domain name.
    /// </summary>
    public string UserDomainName => Environment.UserDomainName;
    
    /// <summary>
    /// Gets whether the current process is running in user-interactive mode.
    /// </summary>
    public bool UserIsInteractive => Environment.UserInteractive;
    
    /// <summary>
    /// Gets the user's local application data directory path.
    /// </summary>
    public string UserLocalApplicationDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Gets the username of the current user.
    /// </summary>
    public string Username => Environment.UserName;
    
    /// <summary>
    /// Returns the platform target triple string.
    /// </summary>
    public override string ToString()
    {
        return Triple;
    }
    
    /// <summary>
    /// Determines if the current operating system is iPadOS.
    /// </summary>
    /// <returns>True if the operating system is iPadOS, otherwise false.</returns>
    private static bool IsIPadOS()
    {
        if (!OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
        {
            return false;
        }

        try
        {
            // On iOS, hw.machine is the device model ("iPad13,8"); hw.model is the board ("J522AP").
            return IsIPadModel(GetSysctlString("hw.machine"), Environment.GetEnvironmentVariable("SIMULATOR_MODEL_IDENTIFIER"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether an Apple model identifier is an iPad. In the simulator, <paramref name="machine"/> is the
    /// host's CPU ("arm64"), so the simulated device's model is used instead.
    /// </summary>
    /// <param name="machine">The <c>hw.machine</c> value.</param>
    /// <param name="simulatorModel">The <c>SIMULATOR_MODEL_IDENTIFIER</c> environment variable, set only in the simulator.</param>
    internal static bool IsIPadModel(string machine, string? simulatorModel)
    {
        string model = string.IsNullOrEmpty(simulatorModel) ? machine : simulatorModel;
        return model.StartsWith("iPad", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSysctlString(string name)
    {
        nint size = 0;
        if (sysctlbyname(name, IntPtr.Zero, ref size, IntPtr.Zero, 0) == 0 && size > 0)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (sysctlbyname(name, buffer, ref size, IntPtr.Zero, 0) == 0)
                {
                    return Marshal.PtrToStringAnsi(buffer)?.TrimEnd('\0') ?? string.Empty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Estimates the number of GPUs on the current machine.
    /// </summary>
    /// <returns>The estimated number of GPUs.</returns>
    private static int EstimateNumberOfGpus()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string? cudaEnv = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES");
                if (!string.IsNullOrWhiteSpace(cudaEnv))
                {
                    return cudaEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
                }

                // Count display adapters via registry if accessible
                try
                {
                    using Microsoft.Win32.RegistryKey? videoKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                    if (videoKey is not null)
                    {
                        int count = 0;
                        foreach (string subKeyName in videoKey.GetSubKeyNames())
                        {
                            if (int.TryParse(subKeyName, out _))
                            {
                                using Microsoft.Win32.RegistryKey? subKey = videoKey.OpenSubKey(subKeyName);
                                if (subKey?.GetValue("DriverDesc") is not null)
                                {
                                    count++;
                                }
                            }
                        }
                        if (count > 0)
                        {
                            return count;
                        }
                    }
                }
                catch
                {
                }

                return 1;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Apple Silicon and Intel Macs have at least 1 GPU
                return 1;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                string? cudaEnv = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES");
                if (!string.IsNullOrWhiteSpace(cudaEnv))
                {
                    return cudaEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
                }

                // Check for DRM card devices (/dev/dri/card*)
                try
                {
                    if (Directory.Exists("/dev/dri"))
                    {
                        string[] cards = Directory.GetFiles("/dev/dri", "card*");
                        if (cards.Length > 0)
                        {
                            return cards.Length;
                        }
                    }
                }
                catch
                {
                }

                // Check for nvidia card devices (/dev/nvidia[0-9]+)
                try
                {
                    if (Directory.Exists("/dev"))
                    {
                        int nvidiaCount = Directory.GetFiles("/dev", "nvidia[0-9]*").Length;
                        if (nvidiaCount > 0)
                        {
                            return nvidiaCount;
                        }
                    }
                }
                catch
                {
                }

                return 1;
            }

            // Default assumption
            return 1;
        }
        catch
        {
            return 1;
        }
    }

    /// <summary>
    /// Gets the total system physical memory.
    /// </summary>
    /// <returns>The total system memory in bytes.</returns>
    private static ulong GetTotalSystemMemory()
    {
        try
        {
            GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
            if (gcInfo.TotalAvailableMemoryBytes > 0)
            {
                return (ulong)gcInfo.TotalAvailableMemoryBytes;
            }

            return DefaultGpuBytes;
        }
        catch
        {
            return DefaultGpuBytes;
        }
    }

    /// <summary>
    /// Gets the estimated total GPU memory.
    /// </summary>
    /// <param name="totalSystemMemory">The total system memory in bytes for unified memory platforms.</param>
    /// <returns>The total GPU memory in bytes.</returns>
    private static ulong GetTotalGpuMemory(ulong totalSystemMemory)
    {
        try
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            {
                // Apple Silicon and modern Apple architectures use unified memory architecture (UMA)
                // where GPU shares the entire system RAM.
                if (totalSystemMemory > 0)
                {
                    return totalSystemMemory;
                }

                ulong sysMem = GetTotalSystemMemory();
                if (sysMem > 0)
                {
                    return sysMem;
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                // Inspect registry for DedicatedVideoMemory if available
                try
                {
                    using Microsoft.Win32.RegistryKey? videoKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                    if (videoKey is not null)
                    {
                        ulong totalGpuMem = 0;
                        foreach (string subKeyName in videoKey.GetSubKeyNames())
                        {
                            if (int.TryParse(subKeyName, out _))
                            {
                                using Microsoft.Win32.RegistryKey? subKey = videoKey.OpenSubKey(subKeyName);
                                object? memValue = subKey?.GetValue("HardwareInformation.qwMemorySize") ?? subKey?.GetValue("HardwareInformation.MemorySize");
                                if (memValue is not null)
                                {
                                    if (memValue is long lVal && lVal > 0)
                                    {
                                        totalGpuMem += (ulong)lVal;
                                    }
                                    else if (memValue is int iVal && iVal > 0)
                                    {
                                        totalGpuMem += (ulong)(uint)iVal;
                                    }
                                    else if (memValue is byte[] bytes && bytes.Length >= 8)
                                    {
                                        totalGpuMem += BitConverter.ToUInt64(bytes, 0);
                                    }
                                    else if (memValue is byte[] bytes4 && bytes4.Length >= 4)
                                    {
                                        totalGpuMem += BitConverter.ToUInt32(bytes4, 0);
                                    }
                                }
                            }
                        }

                        if (totalGpuMem > 0)
                        {
                            return totalGpuMem;
                        }
                    }
                }
                catch
                {
                }
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            {
                // Try sysfs for DRM or AMD/Intel/NVIDIA VRAM
                try
                {
                    ulong totalVram = 0;
                    if (Directory.Exists("/sys/class/drm"))
                    {
                        foreach (string cardDir in Directory.GetDirectories("/sys/class/drm", "card[0-9]*"))
                        {
                            string vramTotalPath = Path.Combine(cardDir, "device", "mem_info_vram_total");
                            if (File.Exists(vramTotalPath))
                            {
                                string content = File.ReadAllText(vramTotalPath).Trim();
                                if (ulong.TryParse(content, out ulong vramBytes) && vramBytes > 0)
                                {
                                    totalVram += vramBytes;
                                }
                            }
                        }

                        if (totalVram > 0)
                        {
                            return totalVram;
                        }
                    }
                }
                catch
                {
                }
            }

            // Fallback: if we have system memory, fallback to a sensible share or default GPU bytes
            if (totalSystemMemory > 0)
            {
                return Math.Min(totalSystemMemory, DefaultGpuBytes);
            }

            return DefaultGpuBytes;
        }
        catch
        {
            return DefaultGpuBytes;
        }
    }

    // libSystem by full path: a bare "libc" isn't resolved on iOS.
    [DllImport("/usr/lib/libSystem.dylib", SetLastError = true)]
    private static extern int sysctlbyname(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr oldp,
        ref IntPtr oldlenp,
        IntPtr newp,
        IntPtr newlen);
}
