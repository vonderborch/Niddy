namespace Niddy.Core.Settings;

/// <summary>
/// What a versioned <see cref="SettingsStore{T}"/> does when an upgrade step throws, or is missing while
/// <see cref="SettingsStore{T}.TreatMissingMigrationAsError"/> is set. Set it with
/// <see cref="SettingsStore{T}.MigrationFailureAction"/>, or decide per failure by overriding
/// <see cref="SettingsStore{T}.OnMigrationFailed"/>.
/// </summary>
public enum SettingsMigrationFailureAction
{
    /// <summary>
    /// Treat the file as unreadable (the default): <see cref="SettingsStore{T}.CorruptFileDetected"/> is raised with
    /// the <see cref="SettingsMigrationException"/>, the file is set aside (renamed to <c>*.bad</c>) if
    /// <see cref="SettingsStore{T}.OverrideCorruptFile"/> is set, and the defaults are used.
    /// </summary>
    UseDefaults,

    /// <summary>
    /// Undo whatever the failed step changed and carry on with the next one, as if the step had nothing to do. The
    /// rest of the upgrade, the backup and the save happen as usual. When <see cref="SettingsStore{T}.OnUpgraded"/>
    /// fails, the settings are deserialized again without it.
    /// </summary>
    SkipStep,

    /// <summary>
    /// Throw the <see cref="SettingsMigrationException"/> to whoever read the settings (<see cref="SettingsStore{T}.Current"/>,
    /// <see cref="SettingsStore{T}.Load"/>, <see cref="SettingsStore{T}.Update"/>, ...). Nothing is set aside or
    /// written, and nothing is loaded, so the next read tries again. A file picked up by
    /// <see cref="SettingsStore{T}.Watch"/> is ignored instead, keeping the current settings.
    /// </summary>
    Throw,
}
