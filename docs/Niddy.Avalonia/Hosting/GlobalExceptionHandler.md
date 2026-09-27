# GlobalExceptionHandler

`Niddy.Avalonia.Hosting` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Hosting/GlobalExceptionHandler.cs)

Catches exceptions nothing else did, on the UI thread, in unobserved tasks and on background threads. It logs them, raises `ExceptionOccurred`, and, for UI-thread exceptions, shows [`Dialog.Exception`](../Dialogs/Dialog.md) and keeps the app running instead of crashing. [`NiddyApp`](NiddyApp.md) installs it for you (configured from `NiddyAppOptions`). Without `NiddyApp`, call `Install` once the UI thread is running.

| Source | Logged as | Dialog by default | App keeps running |
|---|---|---|---|
| `UIThread`: event handlers, `async void` | Error | yes (`ShowDialog`) | yes (`KeepRunning`) |
| `UnobservedTask`: a faulted task nobody awaited, found at GC | Error | no (`ShowDialogForUnobservedTasks`) | yes |
| `AppDomain`: a background thread | Critical | never | **no**. The runtime ends the process; the logger factory is disposed first so the log is flushed. |

Only one exception dialog shows at a time, so a failing timer can't stack up dialogs.

## API

### `GlobalExceptionHandler` (static)

| Member | Description |
|---|---|
| `Install(GlobalExceptionHandlerOptions? options = null)` | Replaces any earlier installation. |
| `Uninstall()` | |
| `bool IsInstalled`, `GlobalExceptionHandlerOptions? Options` | |
| `event EventHandler<UnhandledExceptionOccurredEventArgs> ExceptionOccurred` | Raised before the default handling: on the UI thread for UI-thread exceptions, otherwise on whichever thread found it. |
| `bool Report(Exception exception, UnhandledExceptionSource source = UIThread)` | Handles an exception you caught yourself as if it were unhandled. Returns whether the app should keep running. |

### `GlobalExceptionHandlerOptions`

| Property | Default |
|---|---|
| `LoggerFactory` | null (no logging) |
| `KeepRunning` | true |
| `ShowDialog` | true |
| `ShowDialogForUnobservedTasks` | false |
| `DialogTitle` | "Something went wrong" |
| `DialogDescription` | "An unexpected error occurred. You can continue, but the app may not behave correctly." |
| `ReportIssueLink` | `""` (button hidden) |

### `UnhandledExceptionOccurredEventArgs`

| Member | Description |
|---|---|
| `Exception`, `Source` | |
| `IsTerminating` | True for `AppDomain`. |
| `bool ShowDialog { get; set; }` | Starts as the option for this source; set false to show your own UI. |
| `bool Handled { get; set; }` | True skips the dialog (the exception is still logged). |

## Examples

With `NiddyApp`:

```csharp
options.LoggerFactory = NiddyLogging.CreateFactory(AppPaths.For("MyApp"));
options.ReportIssueLink = "https://github.com/me/myapp/issues/new";
options.OnUnhandledException = e =>
{
    if (e.Exception is HttpRequestException)
    {
        e.ShowDialog = false;
        Toast.Show(NiddyApp.Current!.Shell, "You're offline.", ToastType.Warning);
    }
};
```

Without `NiddyApp`:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    GlobalExceptionHandler.Install(new GlobalExceptionHandlerOptions
    {
        LoggerFactory = loggerFactory,
        ReportIssueLink = "https://github.com/me/myapp/issues/new",
    });
    base.OnFrameworkInitializationCompleted();
}
```

Fire-and-forget work:

```csharp
_ = Task.Run(async () =>
{
    try { await SyncAsync(); }
    catch (Exception ex) { GlobalExceptionHandler.Report(ex, UnhandledExceptionSource.UnobservedTask); }
});
```

## See also

- [NiddyLogging](../../Niddy.Core/Logging/NiddyLogging.md), [Dialog](../Dialogs/Dialog.md)
