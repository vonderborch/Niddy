namespace Niddy.Core.SystemInfo;

/// <summary>
/// Thrown by <see cref="Platform.Info"/> when the app declared its supported operating systems with
/// <see cref="Platform.DeclareSupportedOperatingSystems"/> and is running on a different one.
/// </summary>
public sealed class UnsupportedOperatingSystemException : PlatformNotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedOperatingSystemException"/> class.
    /// </summary>
    /// <param name="operatingSystem">The detected operating system.</param>
    /// <param name="supportedOperatingSystems">The operating systems the app declared it supports.</param>
    public UnsupportedOperatingSystemException(OS operatingSystem, IReadOnlySet<OS> supportedOperatingSystems)
        : base($"{operatingSystem} isn't supported. Supported: {string.Join(", ", supportedOperatingSystems.Order())}.")
    {
        OperatingSystem = operatingSystem;
        SupportedOperatingSystems = supportedOperatingSystems;
    }

    /// <summary>
    /// Gets the detected operating system.
    /// </summary>
    public OS OperatingSystem { get; }

    /// <summary>
    /// Gets the operating systems the app declared it supports.
    /// </summary>
    public IReadOnlySet<OS> SupportedOperatingSystems { get; }
}
