# Platform

`Niddy.Core.SystemInfo` · Niddy.Core · [source](../../../src/Niddy.Core/SystemInfo/Platform.cs)

Information about the machine the app is running on: operating system and its version, process architecture, .NET runtime, a target triple (`aarch64-apple-darwin`, `x86_64-pc-windows-msvc`), memory and an estimate of the GPUs. Use it for diagnostics, "About" screens, bug reports, and choosing native binaries or model sizes.

`Platform.Info` is detected once, on first access, and cached. The OS, architecture, triple, memory and GPU values are fixed from then on. The environment properties (`CurrentDirectory`, `MachineName`, `Username` and so on) read `Environment` every time they're called.

The OS is chosen by checking each platform in turn, and the first match wins. Browser and WASI are checked first, then Android before Linux, and Mac Catalyst, watchOS, tvOS and iPadOS before iOS, because each also reports as the more general one. iPadOS is recognized from the device model (`hw.machine`, e.g. `iPad13,8`), or the simulated device's model in the simulator.

## API

| Member | Description |
|---|---|
| `static Platform Info` | The current machine, detected on first access. Throws [`UnsupportedOperatingSystemException`](#unsupportedoperatingsystemexception) on every access if the app declared its supported OSes and this isn't one of them. |
| `static IReadOnlySet<OS>? SupportedOperatingSystems` | The OSes the app declared it supports, or null if it hasn't declared any (everything is allowed). |
| `static void DeclareSupportedOperatingSystems(params IEnumerable<OS> supported)` | Declares the supported OSes. From then on `Info` throws on any other OS. If `Info` was already detected, checks right away and throws. A later call replaces the earlier one. Throws `ArgumentException` if `supported` is empty. |
| `OS OS` | The detected operating system; `OS.Unknown` if none matched. |
| `Version OSVersion` | The OS version from `Environment.OSVersion.Version`. On Windows it's the NT version (`10.0.22631` on Windows 11), on macOS and iOS the product version (`15.0`), on Linux and Android the **kernel** version (`6.8.0`). On WebAssembly (browser or WASI) it doesn't identify the browser, the WASI host or the OS under them. |
| `string OperatingSystemVersion` | The OS version as a display string, e.g. `"Unix 15.0.0"` or `"Microsoft Windows NT 10.0.22631.0"`. |
| `Architecture Architecture` | The process architecture (`System.Runtime.InteropServices.Architecture`), as .NET reports it. Every value is accepted; see [Architectures](#architectures). |
| `string Runtime` | The .NET runtime description, e.g. `".NET 10.0.0"`. |
| `string Triple` | A Rust/LLVM-style target triple, `<arch>-<platform>`: see [Architectures](#architectures) and the [OS table](#os). E.g. `aarch64-apple-darwin`, `x86_64-pc-windows-msvc`, `x86_64-unknown-linux-musl`, `armv7-unknown-linux-gnueabihf`. |
| `int TotalGpus` | Estimated GPU count. Uses `CUDA_VISIBLE_DEVICES` if set, then display adapters (Windows registry) or `/dev/dri/card*` and `/dev/nvidia*` (Linux, FreeBSD). Falls back to 1. |
| `ulong TotalMemoryBytes` | Memory available to the process, from `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`, so container limits apply. On WebAssembly this is the WebAssembly heap limit, not the device's memory. Falls back to 4 GB. |
| `ulong TotalGpuMemoryBytes` | Estimated GPU memory. macOS and Mac Catalyst: all of `TotalMemoryBytes` (unified memory). Windows: summed from the display adapter registry keys. Linux, FreeBSD: summed from `/sys/class/drm/card*/device/mem_info_vram_total` (AMD). Otherwise the smaller of `TotalMemoryBytes` and 4 GB. |
| `string CommandLine` | `Environment.CommandLine`. |
| `string CurrentDirectory` | `Directory.GetCurrentDirectory()`. |
| `string MachineName` | `Environment.MachineName`. |
| `int ProcessorCount` | `Environment.ProcessorCount`. |
| `string SystemDirectory` | `Environment.SystemDirectory` (empty on non-Windows). |
| `string UserDocumentsDirectory` | The user's documents folder. |
| `string UserLocalApplicationDataDirectory` | The user's local app data folder. For your app's own folders use [AppPaths](../IO/AppPaths.md). |
| `string UserDomainName` | `Environment.UserDomainName`. |
| `bool UserIsInteractive` | `Environment.UserInteractive`. |
| `string Username` | `Environment.UserName`. |
| `ToString()` | Returns `Triple`. |

### OS

`enum OS`, in the same namespace and [source folder](../../../src/Niddy.Core/SystemInfo/OS.cs). The platform part of `Triple` for each:

| Value | Triple platform |
|---|---|
| `Windows` | `pc-windows-msvc`. .NET on Windows always uses the MSVC ABI, so there's no `windows-gnu` variant. |
| `Linux` | `unknown-linux-gnu`, or `unknown-linux-musl` on musl distributions such as Alpine (detected from `RuntimeInformation.RuntimeIdentifier`). On 32-bit ARM, `eabihf` is added: `unknown-linux-gnueabihf`, `unknown-linux-musleabihf`. |
| `MacOS` | `apple-darwin` |
| `MacCatalyst` | `apple-ios-macabi`: an iPad app running natively on macOS |
| `FreeBSD` | `unknown-freebsd` |
| `Android` | `linux-android`, or `linux-androideabi` on 32-bit ARM |
| `iOS` | `apple-ios` |
| `iPadOS` | `apple-ipados` |
| `WatchOS` | `apple-watchos` |
| `TvOS` | `apple-tvos` |
| `Browser` | `unknown-emscripten` (so the triple is `wasm32-unknown-emscripten`) |
| `Wasi` | `unknown-wasi` (`wasm32-unknown-wasi`): a WASI host outside the browser, such as Wasmtime |
| `Unknown` | `unknown` |

`iPadOS` isn't a standard Rust/LLVM target name (they use `apple-ios` for iPads too). Use `OS` rather than the triple if you need to treat both the same.

### Architectures

The architecture part of `Triple`:

| `Architecture` | Triple arch |
|---|---|
| `X86` | `i686` |
| `X64` | `x86_64` |
| `Arm` (ARMv7) | `armv7` |
| `Armv6` (e.g. Raspberry Pi Zero/1) | `arm` |
| `Arm64` | `aarch64` |
| `Wasm` | `wasm32` |
| `RiscV64` | `riscv64gc` |
| `LoongArch64` | `loongarch64` |
| `S390x` (IBM Z) | `s390x` |
| `Ppc64le` (POWER) | `powerpc64le` |
| anything newer | `unknown` |

### UnsupportedOperatingSystemException

`sealed class UnsupportedOperatingSystemException : PlatformNotSupportedException`, in the same namespace ([source](../../../src/Niddy.Core/SystemInfo/UnsupportedOperatingSystemException.cs)). It's thrown by `Info` and `DeclareSupportedOperatingSystems` when the app is running on an OS it didn't declare. Because it's a `PlatformNotSupportedException`, existing handlers for that type catch it too.

| Member | Description |
|---|---|
| `UnsupportedOperatingSystemException(OS operatingSystem, IReadOnlySet<OS> supportedOperatingSystems)` | |
| `OS OperatingSystem` | The detected OS. |
| `IReadOnlySet<OS> SupportedOperatingSystems` | The OSes the app declared. |
| `Message` | e.g. `"Linux isn't supported. Supported: Windows, MacOS."` |

## Examples

Refuse to run on unsupported OSes, first thing in `Main`:

```csharp
using Niddy.Core.SystemInfo;

public static int Main(string[] args)
{
    try
    {
        Platform.DeclareSupportedOperatingSystems(OS.Windows, OS.MacOS, OS.Linux);
        _ = Platform.Info;   // detect now, so an unsupported OS fails here rather than later
    }
    catch (UnsupportedOperatingSystemException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    // ... start the app
    return 0;
}
```

Once declared, every later `Platform.Info` access on an unsupported OS throws, so a library that reads `Info` deep inside the app fails loudly instead of misbehaving.

Log the environment at startup:

```csharp
using Niddy.Core.SystemInfo;
using Niddy.Core.Text;

Platform info = Platform.Info;
logger.LogInformation("{Triple} {OS} {Version}, {Runtime}, {Cpus} CPUs, {Memory}",
    info.Triple, info.OS, info.OSVersion, info.Runtime, info.ProcessorCount, Humanize.Bytes((long)info.TotalMemoryBytes));
```

Gate a feature on the OS version:

```csharp
using Niddy.Core.SystemInfo;

bool supportsNewApi = Platform.Info.OS == OS.MacOS && Platform.Info.OSVersion >= new Version(14, 0);
```

Pick a native library by triple:

```csharp
using Niddy.Core.SystemInfo;

string nativeDir = Path.Combine(AppContext.BaseDirectory, "native", Platform.Info.Triple);
```

## See also

- [AppPaths](../IO/AppPaths.md): per-platform app folders
- [Humanize](../Text/Humanize.md): `Humanize.Bytes` for showing memory sizes
