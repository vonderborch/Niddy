# Plugin pages

Pages can live in other assemblies: in referenced projects, or in plugins loaded at runtime. It uses [PageRegistry](../Niddy.Avalonia/PageSystem/PageRegistry.md), [PageRegistrationAttribute](../Niddy.Avalonia/PageSystem/PageRegistrationAttribute.md), [PageRegistrationGenerator](../Niddy.Avalonia.Generators/PageRegistrationGenerator.md) and [PageMenu](../Niddy.Avalonia/PageSystem/PageMenu.md).

## How pages are found

Each assembly that uses the generator gets a `Niddy.Generated.Pages_<AssemblyName>` provider, marked with `[assembly: PageProvider(...)]`. The first time the registry is used, it scans the loaded assemblies for those attributes. After that, it watches `AppDomain.AssemblyLoad`, so an assembly loaded later registers its pages as it loads. A provider also includes the providers of the assemblies it references, so pages in referenced projects are there before those assemblies load.

## A shared contracts project

Plugins usually need to name the app's pages (e.g. to add themselves under Settings) and use its data context. Put those in a small project that both the app and the plugins reference:

```
MyApp.Contracts   → Niddy.Avalonia (SettingsPage, MainDataContext, IPluginHost…)
MyApp             → MyApp.Contracts, Niddy.Avalonia.Generators
MyApp.Plugin.Git  → MyApp.Contracts, Niddy.Avalonia.Generators
```

## The plugin

```csharp
using MyApp.Contracts;
using Niddy.Avalonia.PageSystem;

namespace MyApp.Plugin.Git;

// Adds itself under the app's Settings page, and as a top-level page
[PageRegistration(DisplayName = "Git", Parents = [typeof(SettingsPage)])]
[PageIcon(Source = "avares://MyApp.Plugin.Git/Assets/git.png", Size = 24)]
public partial class GitSettingsPage : Page<MainDataContext, GitSettingsPageDataContext>
{
    public GitSettingsPage() => InitializeComponent();
}

[PageRegistration(DisplayName = "Repositories", Shortcut = "Primary+Shift+G")]
public partial class RepositoriesPage : Page<MainDataContext, RepositoriesPageDataContext>
{
    public RepositoriesPage() => InitializeComponent();
}
```

## Loading plugins

```csharp
public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        options.StartingPage = HomePage.PageId;
        options.Layout = pages => new PageMenu { Content = pages, Mode = PageMenuMode.Tree };

        // NiddyApp.Paths isn't available until Configure returns, so build the paths here
        var pluginFolder = Path.Combine(AppPaths.For("MyApp").Data, "plugins");
        if (Directory.Exists(pluginFolder))
            foreach (var dll in Directory.GetFiles(pluginFolder, "*.Plugin.*.dll"))
                Assembly.LoadFrom(dll);   // pages register as the assembly loads
    }
}
```

The menu and every `PageView.AvailablePages` update as pages are registered, so a plugin loaded after startup appears straight away. `PageRegistry.PagesChanged` may be raised on the thread that loaded the assembly. Marshal to the UI thread if you handle it yourself:

```csharp
PageRegistry.PagesChanged += (_, _) => Dispatcher.UIThread.Post(RefreshPluginList);
```

Load plugins into the default load context (`Assembly.LoadFrom`), or share `Niddy.Avalonia` and your contracts assembly with any custom `AssemblyLoadContext`. Otherwise the plugin's `Page` type isn't the app's.

## Without the generator

A plugin can register its pages from its own entry point:

```csharp
public sealed class GitPlugin : IPlugin
{
    public void Start(IServiceProvider services)
    {
        PageRegistry.Register(() => new RepositoriesPage(services.GetRequiredService<IGitService>()),
            displayName: "Repositories");
        PageRegistry.Register<GitSettingsPage>(displayName: "Git", parents: [typeof(SettingsPage)]);
    }
}
```

## See also

- [Generator diagnostics](../Niddy.Avalonia.Generators/Diagnostics.md), [app-setup](app-setup.md)
