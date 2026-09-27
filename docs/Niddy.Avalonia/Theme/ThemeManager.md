# ThemeManager

`Niddy.Avalonia.Theme` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Theme/ThemeManager.cs) · [AppTheme](../../../src/Niddy.Avalonia/Theme/AppTheme.cs)

Tracks whether the app is currently light or dark (the OS theme, or the app's `RequestedThemeVariant`), and tells you when that changes. To *set* the theme, use `Application.RequestedThemeVariant` or `NiddyAppOptions.Theme` (an `AppTheme?`; null follows the OS).

## API

| Member | Description |
|---|---|
| `static AppTheme Current` | `Light` or `Dark`, from `Application.Current.ActualThemeVariant`. `Light` if there's no application. |
| `static bool IsDarkMode` | |
| `static event EventHandler<AppTheme>? ThemeChanged` | Raised with the new theme. |
| `static IObservable<AppTheme> WhenThemeChanged()` | The same as an observable. It doesn't replay the current value. Works with ReactiveUI and System.Reactive without depending on them. |

`AppTheme`: `Light`, `Dark`.

## Example

```csharp
using Niddy.Avalonia.Theme;

Chart.Palette = ThemeManager.IsDarkMode ? DarkPalette : LightPalette;
ThemeManager.ThemeChanged += (_, theme) =>
    Chart.Palette = theme == AppTheme.Dark ? DarkPalette : LightPalette;

// ReactiveUI
ThemeManager.WhenThemeChanged()
    .StartWith(ThemeManager.Current)
    .Subscribe(theme => ViewModel.IsDark = theme == AppTheme.Dark)
    .DisposeWith(disposables);

// Switch the theme (ThemeChanged is raised)
Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
```

## See also

- [NiddyAppOptions](../Hosting/NiddyAppOptions.md)
