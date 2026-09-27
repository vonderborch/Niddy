using System.Text.Json;
using Niddy.IO;
using Niddy.Settings;
using Niddy.Threading;

namespace Niddy.Avalonia.State;

/// <summary>
/// Saves UI state that users expect to be remembered, such as window placements (<see cref="WindowMemory" />) and
/// splitter positions (<see cref="LayoutMemory" />), to a JSON file. Changes are saved shortly after they happen.
/// </summary>
/// <remarks>
/// <see cref="NiddyApp" /> sets <see cref="Default" /> to <c>ui-state.json</c> in the app's data folder
/// (see <see cref="Paths" />) unless <see cref="RememberWindowState" />
/// is false. Without it, set <see cref="Default" /> yourself, or pass a store to <see cref="Attach(Window,string,UiStateStore)" />.
/// </remarks>
public sealed class UiStateStore : SettingsStore<UiState>
{
    private readonly Debouncer _saver;

    /// <summary>The store used when none is given. Null until set, in which case nothing is remembered.</summary>
    public static UiStateStore? Default { get; set; }

    /// <summary>Whether changes are waiting to be saved.</summary>
    public bool HasPendingChanges => _saver.IsPending;

    /// <summary>Creates a store for <paramref name="filePath" />.</summary>
    /// <param name="filePath">The JSON file.</param>
    /// <param name="saveDelay">How long after a change to save; defaults to half a second.</param>
    public UiStateStore(string filePath, TimeSpan? saveDelay = null)
        : base(filePath, (JsonSerializerOptions?)null)
    {
        _saver = new Debouncer(saveDelay ?? TimeSpan.FromMilliseconds(500L), null, captureContext: false);
    }

    /// <summary>Creates a store at <c>ui-state.json</c> in the app's <see cref="Data" /> folder.</summary>
    public static UiStateStore For(AppPaths paths)
    {
        return new UiStateStore(Path.Combine(paths.Data, "ui-state.json"));
    }

    /// <summary>Gets a window's saved placement.</summary>
    public WindowPlacement? GetWindow(string key)
    {
        return Current.Windows.GetValueOrDefault(key);
    }

    /// <summary>Records a window's placement and saves it shortly.</summary>
    public void SetWindow(string key, WindowPlacement placement)
    {
        Current.Windows[key] = placement;
        SaveSoon();
    }

    /// <summary>Gets a layout's saved sizes.</summary>
    public IReadOnlyList<string>? GetLayout(string key)
    {
        return Current.Layouts.GetValueOrDefault(key);
    }

    /// <summary>Records a layout's sizes and saves them shortly.</summary>
    public void SetLayout(string key, IReadOnlyList<string> sizes)
    {
        Current.Layouts[key] = sizes.ToArray();
        SaveSoon();
    }

    /// <summary>Saves pending changes now, e.g. when the app exits.</summary>
    public void Flush()
    {
        _saver.Flush();
    }

    private void SaveSoon()
    {
        try
        {
            _saver.Invoke(base.Save);
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
