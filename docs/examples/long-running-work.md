# Long-running work

Three ways to show work in progress, from most to least intrusive, plus retries and getting the user's attention when it's done. It uses [Dialog.Progress](../Niddy.Avalonia/Dialogs/Dialog.md), [Toast.ShowProgress](../Niddy.Avalonia/Toast/Toast.md), [ToastHandle](../Niddy.Avalonia/Toast/ToastHandle.md), [DataContextBase](../Niddy.Avalonia/DataContexts/DataContextBase.md), [Retry](../Niddy.Core/Helpers/Retry.md) and [WindowNotification](../Niddy.Avalonia/Utilities/WindowNotification.md).

| Use | When |
|---|---|
| `Dialog.Progress` | The user has to wait: nothing else makes sense until it's done (an import, a migration). |
| `Toast.ShowProgress` | The user can keep working (a sync, an upload). |
| `DataContextBase.RunAsync` + `IsBusy` | Work that belongs to one page: a spinner or disabled button on that page. |

> **Progress units differ.** `Dialog.Progress` reports **0–100**. `ToastHandle.Report` takes **0–1**.

## A blocking import with `Dialog.Progress`

The work runs on a background thread, so don't touch controls from it. The result is true if the work finished, and false if it was cancelled or threw.

```csharp
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Toast;
using Niddy.Avalonia.Utilities;
using Niddy.Helpers;

private async void Import_OnClick(object? sender, RoutedEventArgs e)
{
    var files = Directory.GetFiles(_folder, "*.md");

    bool finished = await Dialog.Progress.Open(this, "Importing", $"Importing {files.Length} notes…",
        async (progress, ct) =>
        {
            for (var i = 0; i < files.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                await _library.ImportAsync(files[i], ct);
                progress.Report((i + 1) * 100.0 / files.Length);   // 0–100
            }
        });

    Toast.Show(this, finished ? "Import complete" : "Import cancelled",
        finished ? ToastType.Success : ToastType.Warning);
}
```

## A background sync with `Toast.ShowProgress`

The toast stays until you `Complete` or `Dismiss` it. It's indeterminate by default; pass `isIndeterminate: false` to show a bar. `ToastHandle` methods are safe to call from any thread.

```csharp
private async Task SyncAsync()
{
    using var cts = new CancellationTokenSource();
    var toast = Toast.ShowProgress(this, "Syncing…", isIndeterminate: false,
        cancelText: "Cancel", cancel: cts.Cancel);

    try
    {
        var changes = await _server.GetChangesAsync(cts.Token);
        for (var i = 0; i < changes.Count; i++)
        {
            // Flaky network: try each change up to 5 times with backoff
            await Retry.ExecuteAsync(() => _store.ApplyAsync(changes[i], cts.Token),
                maxRetries: 5, cancellationToken: cts.Token);
            toast.Report((i + 1) / (double)changes.Count);    // 0–1
        }
        toast.Complete($"Synced {changes.Count} changes");
    }
    catch (OperationCanceledException)
    {
        toast.Complete("Sync cancelled", ToastType.Warning);
    }
    catch (Exception ex)
    {
        toast.Complete($"Sync failed: {ex.Message}", ToastType.Error);
    }

    WindowNotification.RequestAttention();   // bounce the dock icon / flash the taskbar if the app isn't in front
}
```

`Retry.ExecuteAsync` retries on **any** exception, including `OperationCanceledException`. The token only cancels the delays between attempts, so make the action itself honor it too.

## Page-level work with `DataContextBase.RunAsync`

`RunAsync` sets `IsBusy` while the work runs. If it throws, `ErrorMessage` is set, or `onError` is called if you pass it.

```csharp
public sealed class ReportsPageDataContext(IReportService reports) : DataContextBase
{
    public ObservableCollection<Report> Items { get; } = [];

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var latest = await Retry.ExecuteAsync(() => reports.GetAllAsync());
        Items.Clear();
        foreach (var report in latest)
            Items.Add(report);
    });
}
```

```xml
<Grid>
  <ListBox ItemsSource="{Binding Items}" IsEnabled="{Binding !IsBusy}" />
  <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" VerticalAlignment="Top" />
  <TextBlock Text="{Binding ErrorMessage}" IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
             Foreground="{DynamicResource SystemFillColorCriticalBrush}" VerticalAlignment="Bottom" />
</Grid>
```

## Fire-and-forget

If you start work without awaiting it, report its failures the same way unhandled exceptions are reported:

```csharp
_ = Task.Run(async () =>
{
    try { await _indexer.RebuildAsync(); }
    catch (Exception ex) { GlobalExceptionHandler.Report(ex, UnhandledExceptionSource.UnobservedTask); }
});
```

## See also

- [GlobalExceptionHandler](../Niddy.Avalonia/Hosting/GlobalExceptionHandler.md), [Debouncer](../Niddy.Core/Threading/Debouncer.md)
