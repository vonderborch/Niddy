namespace Niddy.Core.SystemInfo;

/// <summary>
/// Represents the operating system of the current machine.
/// </summary>
public enum OS
{
    /// <summary>
    /// Windows operating system.
    /// </summary>
    Windows,

    /// <summary>
    /// Linux operating system.
    /// </summary>
    Linux,

    /// <summary>
    /// macOS operating system.
    /// </summary>
    MacOS,

    /// <summary>
    /// FreeBSD operating system.
    /// </summary>
    FreeBSD,

    /// <summary>
    /// Android operating system.
    /// </summary>
    Android,

    /// <summary>
    /// iOS operating system.
    /// </summary>
    iOS,

    /// <summary>
    /// iPadOS operating system.
    /// </summary>
    iPadOS,

    /// <summary>
    /// watchOS operating system.
    /// </summary>
    WatchOS,

    /// <summary>
    /// tvOS operating system.
    /// </summary>
    TvOS,

    /// <summary>
    /// Mac Catalyst: an iPad app running natively on macOS.
    /// </summary>
    MacCatalyst,

    /// <summary>
    /// A web browser, running on WebAssembly.
    /// </summary>
    Browser,

    /// <summary>
    /// A WASI (WebAssembly System Interface) host outside the browser, such as Wasmtime.
    /// </summary>
    Wasi,

    /// <summary>
    /// Unknown operating system.
    /// </summary>
    Unknown
}
