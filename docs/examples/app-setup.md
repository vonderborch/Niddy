# App setup, end to end

A complete small app: a `NiddyApp` with an app-wide data context, three pages in a tree, a side menu, logging, and settings. It uses [NiddyApp](../Niddy.Avalonia/Hosting/NiddyApp.md), [NiddyAppOptions](../Niddy.Avalonia/Hosting/NiddyAppOptions.md), [Page](../Niddy.Avalonia/PageSystem/Page.md), [PageRegistrationAttribute](../Niddy.Avalonia/PageSystem/PageRegistrationAttribute.md), [PageMenu](../Niddy.Avalonia/PageSystem/PageMenu.md), [DataContextBase](../Niddy.Avalonia/DataContexts/DataContextBase.md), [SettingsStore](../Niddy.Core/Settings/SettingsStore.md) and [AppPaths](../Niddy.Core/IO/AppPaths.md).

```bash
dotnet add package Niddy.Avalonia
dotnet add package Niddy.Avalonia.Generators
```

## App.cs

No App.axaml is needed.

```csharp
using Avalonia.Animation;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;
using Niddy.IO;
using Niddy.Logging;

namespace Notes;

public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        var paths = AppPaths.For("Notes").EnsureCreated();

        options.AppName = "Notes";
        options.Title = "Notes";
        options.MinWidth = 400;
        options.StartingPage = HomePage.PageId;
        options.AppDataContext = new MainDataContext(paths);
        options.PageTransition = new CrossFade(TimeSpan.FromMilliseconds(150));
        options.Layout = pages => new PageMenu { Content = pages, Mode = PageMenuMode.Tree };
        options.LoggerFactory = NiddyLogging.CreateFactory(paths);
        options.ReportIssueLink = "https://github.com/me/notes/issues/new";
    }
}
```

## Program.cs (desktop)

```csharp
using Avalonia;

namespace Notes;

public static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
```

## The app-wide data context

One object, shared by every page as `AppDataContext`.

```csharp
using Niddy.IO;
using Niddy.Settings;

namespace Notes;

public sealed class NotesSettings
{
    public int FontSize { get; set; } = 14;
    public bool ConfirmDelete { get; set; } = true;
}

public sealed class NotesSettingsStore(AppPaths paths)
    : SettingsStore<NotesSettings>(Path.Combine(paths.Config, "settings.json"));

public sealed class MainDataContext
{
    public MainDataContext(AppPaths paths)
    {
        Paths = paths;
        Settings = new NotesSettingsStore(paths);
        Settings.Load();
    }

    public AppPaths Paths { get; }
    public NotesSettingsStore Settings { get; }
}
```

## Pages

```csharp
using System.Collections.ObjectModel;
using Niddy.Avalonia.DataContexts;
using Niddy.Avalonia.PageSystem;
using Niddy.IO;
using ReactiveUI;

namespace Notes;

[PageRegistration(DisplayName = "Home", Shortcut = "Primary+1")]
[PageIcon("M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z", Size = 24)]
public partial class HomePage : Page<MainDataContext, HomePageDataContext>
{
    public HomePage() => InitializeComponent();

    protected override HomePageDataContext CreatePageDataContext() => new(AppDataContext.Paths);

    protected override async void OnNavigatedTo(PageNavigationEventArgs e) => await PageDataContext.LoadAsync();
}

public sealed class HomePageDataContext(AppPaths paths) : DataContextBase
{
    public ObservableCollection<string> Notes { get; } = [];

    public Task LoadAsync() => RunAsync(async () =>
    {
        Notes.Clear();
        foreach (var file in await Task.Run(() => Directory.GetFiles(paths.Data, "*.md")))
            Notes.Add(Path.GetFileNameWithoutExtension(file));
    });
}

[PageRegistration(DisplayName = "Settings", KeepAlive = true, Shortcut = "Primary+Shift+S",
    Children = [typeof(AppearanceSettingsPage)])]
public partial class SettingsPage : Page<MainDataContext, SettingsPageDataContext>
{
    public SettingsPage() => InitializeComponent();
}

public sealed class SettingsPageDataContext;

[PageRegistration(DisplayName = "Appearance")]
public partial class AppearanceSettingsPage : Page<MainDataContext, AppearanceSettingsPageDataContext>
{
    public AppearanceSettingsPage() => InitializeComponent();

    protected override AppearanceSettingsPageDataContext CreatePageDataContext() => new(AppDataContext.Settings);
}

public sealed class AppearanceSettingsPageDataContext(NotesSettingsStore store) : ReactiveObject
{
    public int FontSize
    {
        get => store.Current.FontSize;
        set
        {
            store.Update(s => s.FontSize = value);
            this.RaisePropertyChanged();
        }
    }
}
```

`HomePage.axaml`:

```xml
<pageSystem:Page xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:pageSystem="clr-namespace:Niddy.Avalonia.PageSystem;assembly=Niddy.Avalonia"
                 xmlns:niddy="https://github.com/vonderborch/Niddy"
                 xmlns:local="clr-namespace:Notes"
                 x:Class="Notes.HomePage"
                 x:DataType="local:HomePageDataContext">
    <Panel>
        <ListBox ItemsSource="{Binding Notes}" />
        <TextBlock Text="No notes yet" HorizontalAlignment="Center" VerticalAlignment="Center"
                   IsVisible="{Binding Notes.Count, Converter={x:Static niddy:CollectionConverters.IsEmpty}}" />
        <ProgressBar IsIndeterminate="True" VerticalAlignment="Top" IsVisible="{Binding IsBusy}" />
    </Panel>
</pageSystem:Page>
```

`SettingsPage.axaml` shows its children in a nested page view with its own menu:

```xml
<pageSystem:Page ... x:Class="Notes.SettingsPage">
    <pageSystem:PageMenu Style="HorizontalStrip" Placement="TopCenter">
        <pageSystem:PageView StartingPage="{x:Type local:AppearanceSettingsPage}" />
    </pageSystem:PageMenu>
</pageSystem:Page>
```

## Notes

- The files using `Page` derive from the generic `Page<,>` in code-behind. If a file derives from the non-generic `Page` and also imports `Avalonia.Controls`, add `using Page = Niddy.Avalonia.PageSystem.Page;`.
- Without the generator, register the pages yourself in `Configure`: `PageRegistry.Register<HomePage>(displayName: "Home", shortcut: "Primary+1");` etc.
- Window size and position are remembered automatically (`RememberWindowState`).

## See also

- [settings-logging-paths](settings-logging-paths.md), [mobile-and-browser](mobile-and-browser.md)
