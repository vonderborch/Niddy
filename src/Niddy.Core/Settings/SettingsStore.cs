using System.Text.Json;
using Niddy.Helpers;
using Niddy.IO;

namespace Niddy.Settings;

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

/// <summary>
/// Typed settings kept in a JSON file. Derive a class that names the file, then use <see cref="Current"/> to read and
/// <see cref="Update"/> to change and save:
/// <code>
/// public sealed class AppSettingsStore(AppPaths paths)
///     : SettingsStore&lt;AppSettings&gt;(Path.Combine(paths.Config, "settings.json"));
///
/// var store = new AppSettingsStore(paths);
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
    private readonly JsonSerializerOptions _options;
    private T? _current;
    private string? _lastWritten;
    private DebouncedFileWatcher? _watcher;

    /// <summary>Creates a store for <paramref name="filePath"/>.</summary>
    /// <param name="filePath">The JSON file. Its folder is created when first saving.</param>
    /// <param name="options">The JSON options; defaults to <see cref="JsonHelpers.DefaultOptions"/>.</param>
    protected SettingsStore(string filePath, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _options = options ?? JsonHelpers.DefaultOptions;
    }

    /// <summary>The full path of the JSON file.</summary>
    public string FilePath { get; }

    /// <summary>The current settings, loaded from the file the first time they're read.</summary>
    public T Current
    {
        get
        {
            lock (_gate)
                return _current ??= Read();
        }
    }

    /// <summary>Whether <see cref="Watch"/> is reloading the settings when the file changes.</summary>
    public bool IsWatching => _watcher is not null;

    /// <summary>
    /// Raised after the settings change through <see cref="Update"/>, <see cref="Reset"/> or <see cref="Load"/>, or
    /// because the file changed on disk while being watched. When watching, it may be raised on another thread.
    /// </summary>
    public event EventHandler<SettingsChangedEventArgs<T>>? Changed;

    /// <summary>(Re)reads the file, replacing <see cref="Current"/>, and raises <see cref="Changed"/>.</summary>
    /// <returns>The loaded settings.</returns>
    public T Load()
    {
        T settings;
        lock (_gate)
            settings = _current = Read();
        OnChanged(settings, SettingsChangeSource.Load);
        return settings;
    }

    /// <summary>Writes <see cref="Current"/> to the file.</summary>
    public void Save()
    {
        lock (_gate)
            Write(_current ??= Read());
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
        lock (_gate)
        {
            settings = _current ??= Read();
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
        lock (_gate)
        {
            settings = _current = Prepare(CreateDefault());
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
        lock (_gate)
        {
            if (_watcher is not null)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            _watcher = DebouncedFileWatcher.ForFile(FilePath, delay, captureContext: false);
            _watcher.Changed += (_, _) => ReloadIfChangedExternally();
        }
    }

    /// <summary>Stops watching the file.</summary>
    public void StopWatching()
    {
        lock (_gate)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    /// <summary>Stops watching the file.</summary>
    public void Dispose()
    {
        StopWatching();
        GC.SuppressFinalize(this);
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
        Changed?.Invoke(this, new SettingsChangedEventArgs<T>(settings, source));

    internal void ReloadIfChangedExternally()
    {
        T settings;
        lock (_gate)
        {
            string? text;
            try
            {
                text = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
            }
            catch (IOException)
            {
                return; // Still being written; another event will follow.
            }

            if (text is null || text == _lastWritten)
                return;
            settings = _current = Parse(text);
        }
        OnChanged(settings, SettingsChangeSource.Load);
    }

    private T Read()
    {
        string text;
        try
        {
            if (!File.Exists(FilePath))
                return Prepare(CreateDefault());
            text = File.ReadAllText(FilePath);
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
            return Prepare(CreateDefault());
        try
        {
            var settings = JsonSerializer.Deserialize<T>(text, _options) ?? CreateDefault();
            _lastWritten = text;
            return Prepare(settings);
        }
        catch (JsonException)
        {
            SetAside();
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
        var text = JsonSerializer.Serialize(settings, _options);
        _lastWritten = text;
        AtomicFile.WriteAllText(FilePath, text);
    }

    private void SetAside()
    {
        try
        {
            File.Move(FilePath, FilePath + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leave it; it'll be overwritten by the next save.
        }
    }
}
