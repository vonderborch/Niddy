# DataContextBase

`Niddy.Avalonia.DataContexts` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/DataContexts/DataContextBase.cs)

A base class for a view's data context (a view-model, in MVVM terms), built on ReactiveUI's `ReactiveObject`. Name the class after its view, e.g. `SettingsPage` and `SettingsPageDataContext`. It provides `IsBusy` and `ErrorMessage` state, and a `RunAsync` helper that manages both.

## API

| Member | Description |
|---|---|
| `bool IsBusy { get; protected set; }` | True while a `RunAsync` call is running. |
| `string? ErrorMessage { get; protected set; }` | The message from the last failed `RunAsync`. Cleared when a run starts. |
| `protected Task RunAsync(Func<Task> work, Action<Exception>? onError = null)` | Sets `IsBusy`, runs `work`, and catches exceptions. On failure it calls `onError`, or, without one, sets `ErrorMessage`. |
| `protected Task<T?> RunAsync<T>(Func<Task<T>> work, Action<Exception>? onError = null)` | The same, returning the result, or `default` on failure. |

`work` runs on the calling context (usually the UI thread). Use `Task.Run` inside it for CPU-heavy work. Calls don't nest: `IsBusy` goes false when any call finishes.

## Example

```csharp
using Niddy.Avalonia.DataContexts;
using ReactiveUI;

public sealed class ProjectsPageDataContext(IProjectService service) : DataContextBase
{
    public ObservableCollection<Project> Projects { get; } = [];

    public string Filter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public Task LoadAsync() => RunAsync(async () =>
    {
        Projects.Clear();
        foreach (var project in await service.GetProjectsAsync(Filter))
            Projects.Add(project);
    });

    public async Task<bool> DeleteAsync(Project project) =>
        await RunAsync(async () => { await service.DeleteAsync(project); return true; },
                       ex => ErrorMessage = $"Couldn't delete {project.Name}: {ex.Message}");
}
```

```xml
<Panel>
    <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" />
    <TextBlock Text="{Binding ErrorMessage}" Foreground="Red"
               IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
</Panel>
```

## See also

- [Page](../PageSystem/Page.md), [examples/long-running-work](../../examples/long-running-work.md)
