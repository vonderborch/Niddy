using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Niddy.Core.Serialization;

namespace Niddy.Core.Settings;

/// <summary>
/// The upgrade steps of a versioned <see cref="SettingsStore{T}"/>: one lambda per version, each upgrading the settings
/// JSON from that version to the next. Add them by overriding <see cref="SettingsStore{T}.ConfigureMigrations"/>:
/// <code>
/// protected override void ConfigureMigrations(SettingsMigrations migrations) =&gt; migrations
///     .Add(fromVersion: 0, json =&gt; json.RenameProperty("colour", "color"))
///     .Add(fromVersion: 1, json =&gt; json.Remove("legacyMode"));
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// When an older file is read, the store runs the step for each version from the file's up to
/// <see cref="SettingsStore{T}.Version"/>, in order, before the JSON is deserialized. A file saved at version 1, read by
/// a store at version 5, goes through the steps from 1, 2, 3 and 4. A version without a step is skipped, or, with
/// <see cref="SettingsStore{T}.TreatMissingMigrationAsError"/>, treated as a failure.
/// </para>
/// <para>
/// Steps work on the JSON rather than on the settings class because an old file often doesn't fit the current class.
/// Property names are as they appear in the file (camelCase with the default options). Use <c>RenameProperty</c> (see
/// <see cref="JsonExtensions"/>) to rename one, and <see cref="JsonObject.Remove(string)"/>, the indexer,
/// <see cref="JsonNode.GetValue{T}"/> and so on for the rest. The version property has already been removed.
/// </para>
/// </remarks>
public sealed class SettingsMigrations
{
    private readonly SortedDictionary<int, Action<JsonObject>> _steps = [];
    private bool _isLocked;

    // Only the store creates one, for ConfigureMigrations.
    internal SettingsMigrations()
    {
    }

    /// <summary>The number of steps added.</summary>
    public int Count => this._steps.Count;

    /// <summary>The versions that have a step, lowest first.</summary>
    public IEnumerable<int> FromVersions => this._steps.Keys;

    /// <summary>
    /// Adds the step that upgrades the settings JSON from <paramref name="fromVersion"/> to
    /// <paramref name="fromVersion"/> + 1.
    /// </summary>
    /// <param name="fromVersion">The version the step upgrades from: 0 for files written before versioning was turned
    /// on. It must be lower than <see cref="SettingsStore{T}.Version"/>.</param>
    /// <param name="step">Changes the JSON, which is at <paramref name="fromVersion"/>, in place. If it throws,
    /// <see cref="SettingsStore{T}.OnMigrationFailed"/> decides what happens.</param>
    /// <returns>This, to chain more steps.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fromVersion"/> is negative.</exception>
    /// <exception cref="ArgumentException">There's already a step from <paramref name="fromVersion"/>.</exception>
    /// <exception cref="InvalidOperationException">Called after <see cref="SettingsStore{T}.ConfigureMigrations"/>
    /// returned.</exception>
    public SettingsMigrations Add(int fromVersion, Action<JsonObject> step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fromVersion);
        ArgumentNullException.ThrowIfNull(step);
        if (this._isLocked)
        {
            throw new InvalidOperationException("Add the steps in ConfigureMigrations; they can't change afterwards.");
        }

        if (!this._steps.TryAdd(fromVersion, step))
        {
            throw new ArgumentException($"There's already a step from version {fromVersion}.", nameof(fromVersion));
        }

        return this;
    }

    /// <summary>Whether there's a step from <paramref name="fromVersion"/>.</summary>
    /// <param name="fromVersion">The version the step would upgrade from.</param>
    public bool Contains(int fromVersion) => this._steps.ContainsKey(fromVersion);

    internal bool TryGet(int fromVersion, [NotNullWhen(true)] out Action<JsonObject>? step) =>
        this._steps.TryGetValue(fromVersion, out step);

    // Stops further changes, and checks every step can run: one from Version or above never would.
    internal void Lock(int version)
    {
        this._isLocked = true;
        int? highest = this._steps.Count > 0 ? this._steps.Keys.Max() : null;
        if (highest >= version)
        {
            throw new InvalidOperationException(
                $"There's a settings upgrade step from version {highest}, but the store's Version is {version}, so it would never run. Raise Version to {highest + 1}.");
        }
    }
}
