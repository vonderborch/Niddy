using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Niddy.Avalonia.State;

/// <summary>
/// Remembers where a window was, how big it was and whether it was maximized, and restores it the next time a window
/// with the same key opens. A saved position that is no longer on any screen (e.g. a monitor was unplugged) is ignored,
/// so the window opens where it normally would.
/// </summary>
/// <remarks>
/// Uses <see cref="UiStateStore.Default" />, which <see cref="Hosting.NiddyApp" /> sets up (and uses for the main
/// window) when <see cref="Hosting.NiddyAppOptions.RememberWindowState" /> is true. Set the key before the window is shown.
/// </remarks>
/// <example>
/// <code>
/// &lt;Window state:WindowMemory.Key="Settings" ...&gt;
/// </code>
/// </example>
public static class WindowMemory
{
    private sealed class Tracker : IDisposable
    {
        private readonly Window _window;

        private readonly string _key;

        private readonly UiStateStore _store;

        private WindowPlacement _placement;

        public Tracker(Window window, string key, UiStateStore store)
        {
            _window = window;
            _key = key;
            _store = store;
            _placement = store.GetWindow(key) ?? new WindowPlacement(window.Width, window.Height);
            window.PropertyChanged += OnPropertyChanged;
            window.PositionChanged += OnPositionChanged;
            window.Closed += OnClosed;
        }

        public void Dispose()
        {
            _window.PropertyChanged -= OnPropertyChanged;
            _window.PositionChanged -= OnPositionChanged;
            _window.Closed -= OnClosed;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_window.IsVisible)
            {
                if (e.Property == TopLevel.ClientSizeProperty && _window.WindowState == WindowState.Normal)
                {
                    Save(_placement with
                    {
                        Width = _window.ClientSize.Width,
                        Height = _window.ClientSize.Height
                    });
                }
                else if (e.Property == Window.WindowStateProperty && _window.WindowState != WindowState.Minimized)
                {
                    Save(_placement with
                    {
                        State = _window.WindowState
                    });
                }
            }
        }

        private void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (_window.IsVisible && _window.WindowState == WindowState.Normal)
            {
                Save(_placement with
                {
                    X = e.Point.X,
                    Y = e.Point.Y
                });
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Dispose();
            _store.Flush();
        }

        private void Save(WindowPlacement placement)
        {
            if (!(placement == _placement))
            {
                _placement = placement;
                _store.SetWindow(_key, placement);
            }
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    /// <summary>Defines the <c>Key</c> attached property.</summary>
    public static readonly AttachedProperty<string?> KeyProperty;

    private static readonly AttachedProperty<IDisposable?> RegistrationProperty;

    private const int MinVisibleWidth = 100;

    private const int MinVisibleHeight = 40;

    static WindowMemory()
    {
        KeyProperty = AvaloniaProperty.RegisterAttached<Window, string?>("Key", typeof(WindowMemory));
        RegistrationProperty = AvaloniaProperty.RegisterAttached<Window, IDisposable?>("Registration", typeof(WindowMemory));
        KeyProperty.Changed.AddClassHandler<Window>((window, e) =>
        {
            window.GetValue(RegistrationProperty)?.Dispose();
            window.SetValue(RegistrationProperty, e.NewValue is string { Length: > 0 } text ? Attach(window, text) : null);
        });
    }

    /// <summary>Gets the key a window's placement is saved under.</summary>
    public static string? GetKey(Window window)
    {
        return window.GetValue(KeyProperty);
    }

    /// <summary>Sets the key a window's placement is saved under, restoring any saved placement.</summary>
    public static void SetKey(Window window, string? key)
    {
        window.SetValue(KeyProperty, key);
    }

    /// <summary>
    /// Restores the window's saved placement, if any, and saves it as it changes. Call it before showing the window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The key to save the placement under, unique among the app's windows.</param>
    /// <param name="store">The store; defaults to <see cref="UiStateStore.Default" />. If there's none, nothing happens.</param>
    /// <returns>Stops remembering the window when disposed.</returns>
    public static IDisposable Attach(Window window, string key, UiStateStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        store ??= UiStateStore.Default;
        if (store == null)
        {
            return EmptyDisposable.Instance;
        }

        if (store.GetWindow(key) is { } placement)
        {
            Restore(window, placement);
        }

        return new Tracker(window, key, store);
    }

    internal static void Restore(Window window, WindowPlacement placement)
    {
        if (placement.Width > 0.0 && double.IsFinite(placement.Width))
        {
            window.Width = Math.Max(placement.Width, window.MinWidth);
        }
        if (placement.Height > 0.0 && double.IsFinite(placement.Height))
        {
            window.Height = Math.Max(placement.Height, window.MinHeight);
        }
        if (placement is { X: { } x, Y: { } y } && window.Screens is { } screens)
        {
            PixelPoint position = new(x, y);
            double scale = screens.ScreenFromPoint(position)?.Scaling ?? 1.0;
            PixelSize size = PixelSize.FromSize(new Size(window.Width, window.Height), scale);
            if (IsVisible(position, size, screens.All.Select(s => s.WorkingArea)))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = position;
            }
        }
        if (placement.State is WindowState.Maximized or WindowState.FullScreen)
        {
            window.WindowState = placement.State;
        }
    }

    /// <summary>Whether enough of a window's title bar would be on one of the screens for the user to grab it.</summary>
    internal static bool IsVisible(PixelPoint position, PixelSize size, IEnumerable<PixelRect> workingAreas)
    {
        PixelRect titleBar = new(position, new PixelSize(Math.Max(size.Width, 1), Math.Min(Math.Max(size.Height, 1), MinVisibleHeight)));
        foreach (PixelRect workingArea in workingAreas)
        {
            PixelRect visible = workingArea.Intersect(titleBar);
            if (visible.Width >= Math.Min(MinVisibleWidth, titleBar.Width) && visible.Height >= Math.Min(20, titleBar.Height))
            {
                return true;
            }
        }
        return false;
    }
}
