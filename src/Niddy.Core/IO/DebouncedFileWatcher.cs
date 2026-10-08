using Niddy.Core.Threading;

namespace Niddy.Core.IO;

/// <summary>A change to a file or directory, after <see cref="DebouncedFileWatcher"/> has merged repeated events.</summary>
/// <param name="FullPath">The path that changed; for a rename, the new path.</param>
/// <param name="ChangeType">What happened: created, changed, deleted or renamed.</param>
/// <param name="OldFullPath">For a rename, the old path; otherwise null.</param>
public sealed record FileChange(string FullPath, WatcherChangeTypes ChangeType, string? OldFullPath = null);

/// <summary>The changes <see cref="DebouncedFileWatcher.Changed"/> reports.</summary>
public sealed class FileChangesEventArgs(IReadOnlyList<FileChange> changes) : EventArgs
{
    /// <summary>The changes, one per path, in the order the paths first changed.</summary>
    public IReadOnlyList<FileChange> Changes { get; } = changes;
}

/// <summary>
/// Watches a directory or file and reports changes once they settle. <see cref="FileSystemWatcher"/> raises several
/// events for a single save (and editors often write a temporary file and rename it); this waits until no event has
/// arrived for <see cref="Delay"/>, then raises <see cref="Changed"/> once with one merged change per path.
/// </summary>
/// <remarks>
/// Events are raised on the <see cref="SynchronizationContext"/> that was current when the watcher was created (e.g.
/// the UI thread), or on the thread pool if there was none. Watching starts straight away; set <see cref="Enabled"/>
/// to pause it. If the operating system drops events, <see cref="Error"/> is raised with an
/// <see cref="InternalBufferOverflowException"/>, and you should rescan.
/// </remarks>
/// <example>
/// <code>
/// using DebouncedFileWatcher watcher = DebouncedFileWatcher.ForFile(settingsPath);
/// watcher.Changed += (_, _) => ReloadSettings();
/// </code>
/// </example>
public sealed class DebouncedFileWatcher : IDisposable
{
    /// <summary>The default time events must stop arriving for before they are reported.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(250);

    private readonly Lock _gate = new();
    private readonly FileSystemWatcher _watcher;
    private readonly Debouncer _debouncer;
    private readonly SynchronizationContext? _context;
    private readonly List<FileChange> _changes = [];
    private readonly StringComparison _pathComparison = Path.Comparison;

    /// <summary>Watches a directory.</summary>
    /// <param name="directory">The directory to watch. It must exist.</param>
    /// <param name="filter">Which files to watch, e.g. <c>"*.json"</c>. Defaults to all.</param>
    /// <param name="includeSubdirectories">Whether to watch subdirectories too.</param>
    /// <param name="delay">How long events must stop arriving for before they are reported; defaults to <see cref="DefaultDelay"/>.</param>
    /// <param name="captureContext">
    /// Whether to raise events on the current <see cref="SynchronizationContext"/>. When false, they're raised on the thread pool.
    /// </param>
    public DebouncedFileWatcher(
        string directory,
        string filter = "*",
        bool includeSubdirectories = false,
        TimeSpan? delay = null,
        bool captureContext = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this._context = captureContext ? SynchronizationContext.Current : null;
        this._debouncer = new Debouncer(delay ?? DefaultDelay, captureContext: false);
        this._debouncer.Error += (_, ex) => RaiseError(ex);

        this._watcher = new FileSystemWatcher(Path.GetFullPath(directory), filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        this._watcher.Created += (_, e) => Record(new FileChange(e.FullPath, WatcherChangeTypes.Created));
        this._watcher.Changed += (_, e) => Record(new FileChange(e.FullPath, WatcherChangeTypes.Changed));
        this._watcher.Deleted += (_, e) => Record(new FileChange(e.FullPath, WatcherChangeTypes.Deleted));
        this._watcher.Renamed += (_, e) => Record(new FileChange(e.FullPath, WatcherChangeTypes.Renamed, e.OldFullPath));
        this._watcher.Error += (_, e) => RaiseError(e.GetException());
        this._watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Watches a single file, which doesn't have to exist yet. Atomic saves (write a temporary file, then rename it over
    /// the target) are reported as a rename to the file.
    /// </summary>
    /// <param name="filePath">The file to watch. Its directory must exist.</param>
    /// <param name="delay">How long events must stop arriving for before they are reported; defaults to <see cref="DefaultDelay"/>.</param>
    /// <param name="captureContext">
    /// Whether to raise events on the current <see cref="SynchronizationContext"/>. When false, they're raised on the thread pool.
    /// </param>
    public static DebouncedFileWatcher ForFile(string filePath, TimeSpan? delay = null, bool captureContext = true)
    {
        string fullPath = Path.GetFullPath(filePath);
        return new DebouncedFileWatcher(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath), delay: delay, captureContext: captureContext);
    }

    /// <summary>The directory being watched.</summary>
    public string Directory => this._watcher.Path;

    /// <summary>Which files are watched, e.g. <c>"*.json"</c>.</summary>
    public string Filter => this._watcher.Filter;

    /// <summary>How long events must stop arriving for before they are reported.</summary>
    public TimeSpan Delay => this._debouncer.Delay;

    /// <summary>Whether changes are being watched. True from creation; set to false to pause.</summary>
    public bool Enabled
    {
        get => this._watcher.EnableRaisingEvents;
        set => this._watcher.EnableRaisingEvents = value;
    }

    /// <summary>Which kinds of change to watch for. Defaults to names, writes, sizes and creation times.</summary>
    public NotifyFilters NotifyFilter
    {
        get => this._watcher.NotifyFilter;
        set => this._watcher.NotifyFilter = value;
    }

    /// <summary>Raised once changes have settled, with one merged change per path.</summary>
    public event EventHandler<FileChangesEventArgs>? Changed;

    /// <summary>
    /// Raised when watching fails, e.g. with an <see cref="InternalBufferOverflowException"/> when the operating system
    /// dropped events. Rescan if you need to be sure nothing was missed.
    /// </summary>
    public event EventHandler<ErrorEventArgs>? Error;

    /// <summary>Stops watching and drops unreported changes.</summary>
    public void Dispose()
    {
        this._watcher.Dispose();
        this._debouncer.Dispose();
    }

    /// <summary>Adds a change, merging it with an earlier one for the same path, and restarts the wait.</summary>
    internal void Record(FileChange change)
    {
        lock (this._gate)
        {
            Merge(this._changes, change, this._pathComparison);
        }

        try
        {
            this._debouncer.Invoke(Report);
        }
        catch (ObjectDisposedException)
        {
            // An event raced with Dispose.
        }
    }

    /// <summary>Merges <paramref name="change"/> into <paramref name="changes"/>, keeping one entry per path.</summary>
    internal static void Merge(List<FileChange> changes, FileChange change, StringComparison comparison)
    {
        int IndexOf(string? path) => path is null ? -1 : changes.FindIndex(c => string.Equals(c.FullPath, path, comparison));

        if (change.ChangeType == WatcherChangeTypes.Renamed)
        {
            // A file created then renamed is simply created under its new name.
            int oldIndex = IndexOf(change.OldFullPath);
            bool created = oldIndex >= 0 && changes[oldIndex].ChangeType == WatcherChangeTypes.Created;
            if (oldIndex >= 0)
            {
                changes.RemoveAt(oldIndex);
            }

            int existing = IndexOf(change.FullPath);
            if (existing >= 0)
            {
                changes.RemoveAt(existing);
            }

            changes.Add(created ? new FileChange(change.FullPath, WatcherChangeTypes.Created) : change);
            return;
        }

        int index = IndexOf(change.FullPath);
        if (index < 0)
        {
            changes.Add(change);
            return;
        }

        WatcherChangeTypes? merged = (changes[index].ChangeType, change.ChangeType) switch
        {
            (WatcherChangeTypes.Created, WatcherChangeTypes.Changed) => WatcherChangeTypes.Created,
            (WatcherChangeTypes.Created, WatcherChangeTypes.Deleted) => (WatcherChangeTypes?)null,
            (WatcherChangeTypes.Deleted, WatcherChangeTypes.Created) => WatcherChangeTypes.Changed,
            (WatcherChangeTypes.Renamed, WatcherChangeTypes.Changed) => WatcherChangeTypes.Renamed,
            (_, WatcherChangeTypes next) => next,
        };

        if (merged is not { } type)
        {
            changes.RemoveAt(index);
        }
        else
        {
            changes[index] = changes[index] with { ChangeType = type, OldFullPath = type == WatcherChangeTypes.Renamed ? changes[index].OldFullPath : null };
        }
    }

    private void Report()
    {
        FileChange[] changes;
        lock (this._gate)
        {
            changes = [.. this._changes];
            this._changes.Clear();
        }

        if (changes.Length == 0)
        {
            return;
        }

        FileChangesEventArgs args = new(changes);
        Raise(() => this.Changed?.Invoke(this, args));
    }

    private void RaiseError(Exception exception)
    {
        ErrorEventArgs args = new(exception);
        Raise(() => this.Error?.Invoke(this, args));
    }

    private void Raise(Action raise)
    {
        if (this._context is null)
        {
            raise();
        }
        else
        {
            this._context.Post(static state => ((Action)state!)(), raise);
        }
    }
}
