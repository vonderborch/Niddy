using System.Text.Json;
using Niddy.Core.Serialization;
using Niddy.Core.IO;

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
/// to fill in or migrate values after loading.
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
    /// Called after settings are loaded or created, before they become <see cref="Current"/>. Override it to fill in
    /// missing values or migrate old ones.
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

            settings = this._current = Parse(text);
        }
        OnChanged(settings, SettingsChangeSource.Load);
    }

    private T Read()
    {
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
        if (string.IsNullOrWhiteSpace(text))
        {
            return Prepare(CreateDefault());
        }

        try
        {
            T settings = JsonSerializer.Deserialize<T>(text, this._serializationOptions) ?? CreateDefault();
            this._lastWritten = text;
            return Prepare(settings);
        }
        catch (JsonException ex)
        {
            bool isSetAside = false;
            if (this.OverrideCorruptFile)
            {
                isSetAside = SetAside();
            }

            OnCorruptFileDetected(this.FilePath, ex, isSetAside);
            return Prepare(CreateDefault());
        }
    }

    private T Prepare(T settings)
    {
        OnLoaded(settings);
        return settings;
    }

    private void Write(T settings)
    {
        string text = JsonSerializer.Serialize(settings, this._serializationOptions);
        this._lastWritten = text;
        AtomicFile.WriteAllText(this.FilePath, text);
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
