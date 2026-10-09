namespace Niddy.Core.Settings;

/// <summary>
/// An upgrade step of a versioned <see cref="SettingsStore{T}"/> threw, or was missing while
/// <see cref="SettingsStore{T}.TreatMissingMigrationAsError"/> is set. <see cref="Exception.InnerException"/> is what
/// the step threw (an <see cref="InvalidOperationException"/> for a missing step). It's passed to <see cref="SettingsStore{T}.OnMigrationFailed"/>, and then, depending on the
/// <see cref="SettingsMigrationFailureAction"/>, reported through <see cref="SettingsStore{T}.CorruptFileDetected"/>
/// (the default), thrown to whoever read the settings, or not at all.
/// </summary>
public sealed class SettingsMigrationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SettingsMigrationException"/> class.</summary>
    /// <param name="fromVersion">The version the failed step was upgrading from.</param>
    /// <param name="toVersion">The version the failed step was upgrading to.</param>
    /// <param name="innerException">What the step threw.</param>
    public SettingsMigrationException(int fromVersion, int toVersion, Exception innerException)
        : base($"Upgrading the settings from version {fromVersion} to {toVersion} failed: {innerException?.Message}", innerException)
    {
        this.FromVersion = fromVersion;
        this.ToVersion = toVersion;
    }

    /// <summary>The version the failed step was upgrading from.</summary>
    public int FromVersion { get; }

    /// <summary>
    /// The version the failed step was upgrading to: <see cref="FromVersion"/> + 1 for a step from
    /// <see cref="SettingsStore{T}.ConfigureMigrations"/>, or <see cref="SettingsStore{T}.Version"/> for
    /// <see cref="SettingsStore{T}.OnUpgraded"/>.
    /// </summary>
    public int ToVersion { get; }

    /// <summary>
    /// Whether the failure is a missing step rather than one that threw: there's no step from
    /// <see cref="FromVersion"/> in <see cref="SettingsStore{T}.ConfigureMigrations"/> and <see cref="SettingsStore{T}.TreatMissingMigrationAsError"/>
    /// is set.
    /// </summary>
    public bool IsMissingStep { get; private init; }

    internal static SettingsMigrationException MissingStep(int fromVersion) =>
        new(fromVersion, fromVersion + 1, new InvalidOperationException($"There's no step from version {fromVersion} to {fromVersion + 1}."))
        {
            IsMissingStep = true,
        };

    // Set for SettingsMigrationFailureAction.Throw, so the store lets it through instead of using the defaults.
    internal bool Rethrow { get; set; }
}
