using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.IO;
using Niddy.Core.IO.Backups;
using Niddy.Core.Serialization;

namespace Niddy.Core.Settings;

/// <summary>Why <see cref="SettingsStore{T}.Changed"/> was raised.</summary>
public enum SettingsChangeSource
{
    /// <summary><see cref="SettingsStore{T}.Update"/> was called.</summary>
    Update,

    /// <summary><see cref="SettingsStore{T}.Reset"/> was called.</summary>
    Reset,

    /// <summary><see cref="SettingsStore{T}.Load"/> was called, or the file changed on disk while being watched.</summary>
    Load,
}

/// <summary>The settings after a change, for <see cref="SettingsStore{T}.Changed"/>.</summary>
public sealed class SettingsChangedEventArgs<T>(T settings, SettingsChangeSource source) : EventArgs
{
    /// <summary>The settings now current.</summary>
    public T Settings { get; } = settings;

    /// <summary>What caused the change.</summary>
    public SettingsChangeSource Source { get; } = source;
}

/// <summary>Event data for <see cref="SettingsStore{T}.CorruptFileDetected"/>.</summary>
public sealed class SettingsCorruptFileEventArgs(string filePath, Exception exception, bool isSetAside) : EventArgs
{
    /// <summary>The path of the file that could not be parsed.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The exception that was thrown when attempting to parse or read the file.</summary>
    public Exception Exception { get; } = exception;

    /// <summary>Whether the corrupt file was set aside (renamed to <c>*.bad</c>).</summary>
    public bool IsSetAside { get; } = isSetAside;
}

/// <summary>
/// Typed settings kept in a JSON file. Derive a class that names the file, then use <see cref="Current"/> to read and
/// <see cref="Update"/> to change and save:
/// <code>
/// public sealed class AppSettingsStore(AppPaths paths)
///     : SettingsStore&lt;AppSettings&gt;(Path.Combine(paths.Config, "settings.json"));
///
/// AppSettingsStore store = new(paths);
/// store.Update(s =&gt; s.Theme = "Dark");
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// The file is read the first time <see cref="Current"/> is used. A missing file gives <see cref="CreateDefault"/>. A
/// file that can't be read as <typeparamref name="T"/> is renamed to <c>*.bad</c> (so it can be recovered) and the
/// defaults are used. Saves are atomic, so a crash never leaves a half-written file. Override <see cref="OnLoaded"/>
/// to fill in values after loading.
/// </para>
/// <para>
/// Override <see cref="Version"/> to version the file. The store writes the version into the file (as
/// <c>"$version"</c>), and when it reads an older file it upgrades the JSON one version at a time with the steps
/// added in <see cref="ConfigureMigrations"/>, then saves the upgraded file, after backing up the old one:
/// <code>
/// public sealed class AppSettingsStore(AppPaths paths)
///     : SettingsStore&lt;AppSettings&gt;(Path.Combine(paths.Config, "settings.json"))
/// {
///     public override int Version =&gt; 2;
///
///     protected override void ConfigureMigrations(SettingsMigrations migrations) =&gt; migrations
///         .Add(fromVersion: 0, json =&gt; json.RenameProperty("colour", "color"))   // files from before versioning
///         .Add(fromVersion: 1, json =&gt; json.Remove("legacyMode"));
/// }
/// </code>
/// </para>
/// <para>
/// The store is thread-safe, but <see cref="Current"/> is a live object: change it through <see cref="Update"/> so the
/// change is saved and <see cref="Changed"/> is raised. Call <see cref="Watch"/> to reload when another process edits
/// the file.
/// </para>
/// </remarks>
/// <typeparam name="T">The settings class. It needs a parameterless constructor and must be JSON-serializable.</typeparam>
public abstract class SettingsStore<T> : IDisposable where T : class, new()
{
    private readonly Lock _gate = new();
    private readonly JsonSerializerOptions _serializationOptions;
    private T? _current;
    private int? _fileVersion;
    private SettingsMigrations? _migrations;
    private string? _lastWritten;
    private DebouncedFileWatcher? _watcher;

    /// <summary>Creates a store for <paramref name="filePath"/>.</summary>
    /// <param name="filePath">The JSON file. Its folder is created when first saving.</param>
    /// <param name="serializationOptions">The JSON serializationOptions; defaults to <c>JsonSerializerOptions.Niddy</c>.</param>
    /// <param name="overrideCorruptFile">Whether to set aside (rename to <c>*.bad</c>) a corrupt or unparseable settings file when reading it. Defaults to <see langword="true"/>.</param>
    protected SettingsStore(string filePath, JsonSerializerOptions? serializationOptions = null, bool overrideCorruptFile = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.FilePath = Path.GetFullPath(filePath);
        this._serializationOptions = serializationOptions ?? JsonSerializerOptions.Niddy;
        this.OverrideCorruptFile = overrideCorruptFile;
    }

    /// <summary>The full path of the JSON file.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Whether to set aside (rename to <c>*.bad</c>) a corrupt or unparseable settings file when reading it.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool OverrideCorruptFile { get; set; }

    /// <summary>The current settings, loaded from the file the first time they're read.</summary>
    public T Current
    {
        get
        {
            lock (this._gate)
            {
                this._current ??= Read();
                return this._current;
            }
        }
    }

    /// <summary>
    /// The version of the settings this store reads and writes. Override it to version the file; start at 1 and add
    /// one, with a step in <see cref="ConfigureMigrations"/>, whenever the JSON changes in a way the serializer can't absorb (a
    /// renamed, moved or retyped property, or a value that needs converting). Adding a property with a default
    /// doesn't need a new version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default, 0, means unversioned: the file is read and written as plain <typeparamref name="T"/> and
    /// nothing is migrated. When it's above 0, the version is written into the file as <see cref="VersionPropertyName"/>.
    /// A file without one is version 0, so the step from version 0 upgrades files written before versioning was turned
    /// on.
    /// </para>
    /// <para>
    /// A file with a newer version than this (written by a newer copy of the app) is read as it is, without
    /// migrating. Properties <typeparamref name="T"/> doesn't know are dropped, unless it has a
    /// <c>[JsonExtensionData]</c> property to keep them. Saves keep the file's newer version, so the newer app doesn't
    /// upgrade it again. <see cref="FileVersion"/> tells you when this happened.
    /// </para>
    /// </remarks>
    public virtual int Version => 0;

    /// <summary>
    /// The version the file had when it was last read, before upgrading: 0 for a file without one, and null when there
    /// was no file, it couldn't be read, or the store is unversioned (<see cref="Version"/> is 0). Compare it with
    /// <see cref="Version"/> after reading <see cref="Current"/>: lower means the file was upgraded, higher means it was
    /// written by a newer copy of the app.
    /// </summary>
    public int? FileVersion
    {
        get
        {
            lock (this._gate)
            {
                return this._fileVersion;
            }
        }
    }

    /// <summary>
    /// Whether to copy the file before saving an upgraded version over it, to
    /// <c>settings.json.v1-20261008T140312Z.bak</c> (the old version and a timestamp). Defaults to
    /// <see langword="true"/>. Making the copy is best effort: if it fails, the upgrade is saved anyway.
    /// </summary>
    public bool BackupBeforeUpgrade { get; set; } = true;

    /// <summary>
    /// What to do when an upgrade step (see <see cref="ConfigureMigrations"/>) or <see cref="OnUpgraded"/> throws:
    /// <see cref="SettingsMigrationFailureAction.UseDefaults"/> (the default),
    /// <see cref="SettingsMigrationFailureAction.SkipStep"/> or <see cref="SettingsMigrationFailureAction.Throw"/>.
    /// Override <see cref="OnMigrationFailed"/> to decide per failure instead.
    /// </summary>
    public SettingsMigrationFailureAction MigrationFailureAction { get; set; } = SettingsMigrationFailureAction.UseDefaults;

    /// <summary>
    /// Whether a version with no step in <see cref="ConfigureMigrations"/> is a failure. When
    /// <see langword="false"/> (the default), that version has nothing to change and is skipped. When
    /// <see langword="true"/>, it's handled like a step that threw: <see cref="OnMigrationFailed"/> gets a
    /// <see cref="SettingsMigrationException"/> whose <see cref="SettingsMigrationException.IsMissingStep"/> is set,
    /// and <see cref="MigrationFailureAction"/> applies. Turn it on to make sure every version bump comes with a step.
    /// </summary>
    public bool TreatMissingMigrationAsError { get; set; }

    /// <summary>Whether <see cref="Watch"/> is reloading the settings when the file changes.</summary>
    public bool IsWatching => this._watcher is not null;

    /// <summary>
    /// Raised after the settings change through <see cref="Update"/>, <see cref="Reset"/> or <see cref="Load"/>, or
    /// because the file changed on disk while being watched. When watching, it may be raised on another thread.
    /// </summary>
    public event EventHandler<SettingsChangedEventArgs<T>>? Changed;

    /// <summary>
    /// Raised when a settings file is found to be corrupt or unparseable.
    /// </summary>
    public event EventHandler<SettingsCorruptFileEventArgs>? CorruptFileDetected;

    /// <summary>(Re)reads the file, replacing <see cref="Current"/>, and raises <see cref="Changed"/>.</summary>
    /// <returns>The loaded settings.</returns>
    public T Load()
    {
        T settings;
        lock (this._gate)
        {
            settings = this._current = Read();
        }

        OnChanged(settings, SettingsChangeSource.Load);
        return settings;
    }

    /// <summary>Writes <see cref="Current"/> to the file.</summary>
    public void Save()
    {
        lock (this._gate)
        {
            Write(this._current ??= Read());
        }
    }

    /// <summary>Writes <see cref="Current"/> to the file without blocking the calling thread on disk.</summary>
    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Save, cancellationToken);

    /// <summary>Changes the settings, saves them and raises <see cref="Changed"/>.</summary>
    /// <param name="change">Changes the settings object it's given.</param>
    /// <returns>The changed settings.</returns>
    public T Update(Action<T> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        T settings;
        lock (this._gate)
        {
            settings = this._current ??= Read();
            change(settings);
            Write(settings);
        }
        OnChanged(settings, SettingsChangeSource.Update);
        return settings;
    }

    /// <summary>Replaces the settings with <see cref="CreateDefault"/>, saves them and raises <see cref="Changed"/>.</summary>
    /// <returns>The new settings.</returns>
    public T Reset()
    {
        T settings;
        lock (this._gate)
        {
            settings = this._current = Prepare(CreateDefault());
            Write(settings);
        }
        OnChanged(settings, SettingsChangeSource.Reset);
        return settings;
    }

    /// <summary>
    /// Starts reloading the settings when the file is changed by something else, such as another instance of the app or
    /// a text editor. The store's own saves are ignored. Does nothing if already watching.
    /// </summary>
    /// <param name="delay">How long the file must stop changing before it's reloaded; defaults to <see cref="DebouncedFileWatcher.DefaultDelay"/>.</param>
    public void Watch(TimeSpan? delay = null)
    {
        lock (this._gate)
        {
            if (this._watcher is not null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this.FilePath)!);
            this._watcher = DebouncedFileWatcher.ForFile(this.FilePath, delay, captureContext: false);
            this._watcher.Changed += (_, _) => ReloadIfChangedExternally();
        }
    }

    /// <summary>Stops watching the file.</summary>
    public void StopWatching()
    {
        lock (this._gate)
        {
            this._watcher?.Dispose();
            this._watcher = null;
        }
    }

    /// <summary>Stops watching the file.</summary>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases what the store holds. Override it to release your own resources (e.g. save pending changes), and call
    /// the base method.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopWatching();
        }
    }

    /// <summary>Creates the settings used when there's no file, or it can't be read, or on <see cref="Reset"/>.</summary>
    protected virtual T CreateDefault() => new();

    /// <summary>
    /// The name of the property that holds the version in the file; <c>"$version"</c> by default. It's written first,
    /// and removed from the JSON before it's deserialized, so <typeparamref name="T"/> never sees it.
    /// </summary>
    /// <remarks>
    /// Override it to take over a version property your settings already have, such as <c>"version"</c>, and then
    /// remove that property from <typeparamref name="T"/>. Use the name as it appears in the file.
    /// </remarks>
    protected virtual string VersionPropertyName => "$version";

    /// <summary>
    /// Adds the upgrade steps of a versioned store: one lambda per version, each upgrading the settings JSON from that
    /// version to the next. It's called once, the first time a file is read; the base adds none.
    /// <code>
    /// protected override void ConfigureMigrations(SettingsMigrations migrations) =&gt; migrations
    ///     .Add(fromVersion: 0, json =&gt; json.RenameProperty("colour", "color"))
    ///     .Add(fromVersion: 1, json =&gt; json["volume"] = json["volume"]!.GetValue&lt;int&gt;() * 10);
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para>
    /// When an older file is read, the step for each version from the file's up to <see cref="Version"/> runs, in
    /// order, before the JSON is deserialized: a file saved at version 1, read by a store at version 5, goes through
    /// the steps from 1, 2, 3 and 4. A version without a step is skipped, or, with
    /// <see cref="TreatMissingMigrationAsError"/>, handled like a step that threw. A step from <see cref="Version"/>
    /// or above can never run, so reading the file throws <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// If a step throws, <see cref="OnMigrationFailed"/> decides what happens. By default the file is treated as
    /// unreadable: <see cref="CorruptFileDetected"/> is raised with a <see cref="SettingsMigrationException"/>, the
    /// file is set aside if <see cref="OverrideCorruptFile"/> is set, and the defaults are used. Changes that are
    /// easier on the typed settings go in <see cref="OnUpgraded"/>.
    /// </para>
    /// </remarks>
    /// <param name="migrations">Add the steps to this.</param>
    protected virtual void ConfigureMigrations(SettingsMigrations migrations)
    {
    }

    /// <summary>
    /// Called once after an older file has been upgraded by the steps from <see cref="ConfigureMigrations"/> and
    /// deserialized, before
    /// <see cref="OnLoaded"/>. Override it for upgrades that are easier on the typed settings, such as working out a
    /// new value from others. If it throws, <see cref="OnMigrationFailed"/> decides what happens, as for a step.
    /// </summary>
    /// <param name="settings">The upgraded settings.</param>
    /// <param name="fromVersion">The version the file had.</param>
    protected virtual void OnUpgraded(T settings, int fromVersion)
    {
    }

    /// <summary>
    /// Called when an upgrade step or <see cref="OnUpgraded"/> throws, or a step is missing and
    /// <see cref="TreatMissingMigrationAsError"/> is set, to decide what happens next.
    /// Returns <see cref="MigrationFailureAction"/> by default. Override it to decide per failure (for example, skip a
    /// step that only tidies up, but give up on one that converts values) or to log the failure.
    /// </summary>
    /// <param name="exception">What failed: <see cref="SettingsMigrationException.FromVersion"/> and
    /// <see cref="SettingsMigrationException.ToVersion"/> say which step, and <see cref="Exception.InnerException"/>
    /// is what it threw. <see cref="SettingsMigrationException.IsMissingStep"/> is set when there was no step.</param>
    /// <returns>What to do. See <see cref="SettingsMigrationFailureAction"/>.</returns>
    protected virtual SettingsMigrationFailureAction OnMigrationFailed(SettingsMigrationException exception) =>
        this.MigrationFailureAction;

    /// <summary>
    /// Called after settings are loaded or created, before they become <see cref="Current"/>. Override it to fill in
    /// missing values. To change the shape of old files, version the store instead (see <see cref="Version"/>).
    /// </summary>
    /// <param name="settings">The loaded settings.</param>
    protected virtual void OnLoaded(T settings)
    {
    }

    /// <summary>Raises <see cref="Changed"/>.</summary>
    protected virtual void OnChanged(T settings, SettingsChangeSource source) =>
        this.Changed?.Invoke(this, new SettingsChangedEventArgs<T>(settings, source));

    /// <summary>Raises <see cref="CorruptFileDetected"/>.</summary>
    /// <param name="filePath">The path of the file that could not be parsed.</param>
    /// <param name="exception">The exception thrown during parsing.</param>
    /// <param name="isSetAside">Whether the corrupt file was set aside (renamed to <c>*.bad</c>).</param>
    protected virtual void OnCorruptFileDetected(string filePath, Exception exception, bool isSetAside) =>
        this.CorruptFileDetected?.Invoke(this, new SettingsCorruptFileEventArgs(filePath, exception, isSetAside));

    internal void ReloadIfChangedExternally()
    {
        T settings;
        lock (this._gate)
        {
            string? text;
            try
            {
                text = File.Exists(this.FilePath) ? File.ReadAllText(this.FilePath) : null;
            }
            catch (IOException)
            {
                return; // Still being written; another event will follow.
            }

            if (text is null || text == this._lastWritten)
            {
                return;
            }

            int? fileVersion = this._fileVersion;
            try
            {
                settings = this._current = Parse(text);
            }
            catch (SettingsMigrationException)
            {
                // SettingsMigrationFailureAction.Throw, with no caller to throw to: keep the current settings.
                this._fileVersion = fileVersion;
                return;
            }
        }
        OnChanged(settings, SettingsChangeSource.Load);
    }

    private T Read()
    {
        this._fileVersion = null;
        string text;
        try
        {
            if (!File.Exists(this.FilePath))
            {
                return Prepare(CreateDefault());
            }

            text = File.ReadAllText(this.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Prepare(CreateDefault());
        }
        return Parse(text);
    }

    private T Parse(string text)
    {
        this._fileVersion = null;
        SettingsMigrations migrations = this.Migrations;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Prepare(CreateDefault());
        }

        T settings;
        int? upgradedFrom = null;
        try
        {
            if (this.Version > 0)
            {
                (settings, upgradedFrom) = DeserializeVersioned(text, migrations);
            }
            else
            {
                settings = JsonSerializer.Deserialize<T>(text, this._serializationOptions) ?? CreateDefault();
            }
        }
        catch (Exception ex) when (ex is JsonException or SettingsMigrationException { Rethrow: false })
        {
            this._fileVersion = null;
            bool isSetAside = false;
            if (this.OverrideCorruptFile)
            {
                isSetAside = SetAside();
            }

            OnCorruptFileDetected(this.FilePath, ex, isSetAside);
            return Prepare(CreateDefault());
        }

        this._lastWritten = text;
        Prepare(settings);
        if (upgradedFrom is { } from)
        {
            SaveUpgrade(settings, from);
        }

        return settings;
    }

    // Built on first use, so ConfigureMigrations isn't called from the constructor. Called under _gate.
    private SettingsMigrations Migrations
    {
        get
        {
            if (this._migrations is null)
            {
                SettingsMigrations migrations = new();
                ConfigureMigrations(migrations);
                migrations.Lock(this.Version);
                this._migrations = migrations;
            }

            return this._migrations;
        }
    }

    private (T Settings, int? UpgradedFrom) DeserializeVersioned(string text, SettingsMigrations migrations)
    {
        JsonNode? node = JsonNode.Parse(text, this.NodeOptions, this.DocumentOptions);
        if (node is null)
        {
            this._fileVersion = 0;
            return (CreateDefault(), null);   // a file that's just "null", like the unversioned path
        }

        if (node is not JsonObject json)
        {
            throw new JsonException($"The settings file must hold a JSON object, not {node.GetValueKind()}.");
        }

        int fileVersion = 0;
        if (json.TryGetPropertyValue(this.VersionPropertyName, out JsonNode? versionNode))
        {
            if (versionNode is not JsonValue value || !value.TryGetValue(out fileVersion) || fileVersion < 0)
            {
                throw new JsonException($"\"{this.VersionPropertyName}\" must be a whole number of 0 or more, not {versionNode?.ToJsonString() ?? "null"}.");
            }

            json.Remove(this.VersionPropertyName);
        }

        this._fileVersion = fileVersion;
        int version = this.Version;
        for (int from = fileVersion; from < version; from++)
        {
            if (!migrations.TryGet(from, out Action<JsonObject>? step))
            {
                if (this.TreatMissingMigrationAsError)
                {
                    HandleMigrationFailure(SettingsMigrationException.MissingStep(from));
                }

                continue;
            }

            JsonObject before = json.DeepClone().AsObject();
            try
            {
                step(json);
            }
            catch (Exception ex)
            {
                HandleMigrationFailure(new SettingsMigrationException(from, from + 1, ex));
                json = before;   // SkipStep: undo what the step did
            }
        }

        T settings = json.Deserialize<T>(this._serializationOptions) ?? CreateDefault();
        if (fileVersion >= version)
        {
            return (settings, null);
        }

        try
        {
            OnUpgraded(settings, fileVersion);
        }
        catch (Exception ex)
        {
            HandleMigrationFailure(new SettingsMigrationException(fileVersion, version, ex));
            settings = json.Deserialize<T>(this._serializationOptions) ?? CreateDefault();   // SkipStep
        }

        return (settings, fileVersion);
    }

    // Returns for SkipStep; throws the failure otherwise, marked to escape Parse for Throw.
    private void HandleMigrationFailure(SettingsMigrationException failure)
    {
        switch (OnMigrationFailed(failure))
        {
            case SettingsMigrationFailureAction.SkipStep:
                return;
            case SettingsMigrationFailureAction.Throw:
                failure.Rethrow = true;
                throw failure;
            default:
                throw failure;
        }
    }

    private JsonNodeOptions NodeOptions => new()
    {
        PropertyNameCaseInsensitive = this._serializationOptions.PropertyNameCaseInsensitive,
    };

    private JsonDocumentOptions DocumentOptions => new()
    {
        AllowTrailingCommas = this._serializationOptions.AllowTrailingCommas,
        // JsonDocument can skip comments but not keep them.
        CommentHandling = this._serializationOptions.ReadCommentHandling == JsonCommentHandling.Disallow
            ? JsonCommentHandling.Disallow
            : JsonCommentHandling.Skip,
        MaxDepth = this._serializationOptions.MaxDepth,
    };

    private void SaveUpgrade(T settings, int fromVersion)
    {
        try
        {
            if (this.BackupBeforeUpgrade)
            {
                File.Backup(this.FilePath, label: $"v{fromVersion}", extension: ".bak");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the upgrade is still saved.
        }

        try
        {
            Write(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The upgraded settings are still current, and the next save writes them.
        }
    }

    private T Prepare(T settings)
    {
        OnLoaded(settings);
        return settings;
    }

    private void Write(T settings)
    {
        string text = Serialize(settings);
        this._lastWritten = text;
        AtomicFile.WriteAllText(this.FilePath, text);
    }

    private string Serialize(T settings)
    {
        int version = this.Version;
        if (version <= 0)
        {
            return JsonSerializer.Serialize(settings, this._serializationOptions);
        }

        if (JsonSerializer.SerializeToNode(settings, this._serializationOptions) is not JsonObject json)
        {
            throw new InvalidOperationException($"A versioned {typeof(T).Name} must serialize to a JSON object.");
        }

        // A file from a newer app keeps its version, so that app doesn't upgrade it again.
        int written = Math.Max(version, this._fileVersion ?? 0);
        string name = this.VersionPropertyName;
        if (json.ContainsKey(name))
        {
            json[name] = written;
        }
        else
        {
            json.Insert(0, name, written);
        }

        return json.ToJsonString(this._serializationOptions);
    }

    private bool SetAside()
    {
        try
        {
            File.Move(this.FilePath, this.FilePath + ".bad", overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leave it; it'll be overwritten by the next save.
            return false;
        }
    }
}
