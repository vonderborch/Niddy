using Avalonia;
using Avalonia.Styling;

namespace Niddy.Avalonia.Theme;

/// <summary>
///     Tracks the OS / application theme variant. Handle <see cref="ThemeChanged"/>, or subscribe to
///     <see cref="WhenThemeChanged"/>, to react to light/dark switches at runtime.
/// </summary>
public static class ThemeManager
{
    private static readonly List<IObserver<AppTheme>> Observers = new();
    private static Application? _trackedApplication;
    private static EventHandler<AppTheme>? _themeChanged;

    /// <summary>The currently active theme.</summary>
    public static AppTheme Current
    {
        get
        {
            EnsureTracking();
            return Resolve();
        }
    }

    /// <summary>True when <see cref="Current"/> is <see cref="AppTheme.Dark"/>.</summary>
    public static bool IsDarkMode => Current == AppTheme.Dark;

    /// <summary>Raised with the new <see cref="AppTheme"/> whenever the OS or application theme changes.</summary>
    public static event EventHandler<AppTheme>? ThemeChanged
    {
        add
        {
            EnsureTracking();
            _themeChanged += value;
        }
        remove => _themeChanged -= value;
    }

    /// <summary>
    ///     An observable that emits the new <see cref="AppTheme"/> whenever the OS or application
    ///     theme changes. Does not replay the current value on subscription. Works with ReactiveUI
    ///     and System.Reactive operators, but doesn't depend on either.
    /// </summary>
    public static IObservable<AppTheme> WhenThemeChanged()
    {
        EnsureTracking();
        return ThemeObservable.Instance;
    }

    // Subscribes lazily rather than in a static constructor, so touching ThemeManager before the
    // Application exists doesn't permanently miss theme changes.
    private static void EnsureTracking()
    {
        var application = Application.Current;
        if (application is null || ReferenceEquals(application, _trackedApplication))
            return;

        if (_trackedApplication is not null)
            _trackedApplication.ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        _trackedApplication = application;
        application.ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private static void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        var theme = Resolve();
        _themeChanged?.Invoke(null, theme);

        IObserver<AppTheme>[] observers;
        lock (Observers)
            observers = Observers.ToArray();
        foreach (var observer in observers)
            observer.OnNext(theme);
    }

    private static AppTheme Resolve() =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? AppTheme.Dark
            : AppTheme.Light;

    private sealed class ThemeObservable : IObservable<AppTheme>
    {
        internal static readonly ThemeObservable Instance = new();

        public IDisposable Subscribe(IObserver<AppTheme> observer)
        {
            lock (Observers)
                Observers.Add(observer);
            return new Subscription(observer);
        }
    }

    private sealed class Subscription(IObserver<AppTheme> observer) : IDisposable
    {
        private IObserver<AppTheme>? _observer = observer;

        public void Dispose()
        {
            var observer = Interlocked.Exchange(ref _observer, null);
            if (observer is null)
                return;
            lock (Observers)
                Observers.Remove(observer);
        }
    }
}
