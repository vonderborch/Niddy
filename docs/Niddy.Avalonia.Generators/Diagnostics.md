# Generator diagnostics

Niddy.Avalonia.Generators · [source](../../src/Niddy.Avalonia.Generators/Diagnostics.cs)

The errors and warnings reported by [`PageRegistrationGenerator`](PageRegistrationGenerator.md), category `Niddy.PageSystem`. A page with an error isn't registered. Without the generator, the same problems throw from `PageRegistry.Register` at runtime.

| Code | Severity | Meaning | Fix |
|---|---|---|---|
| NIDDY001 | Error | Pages' parents and children form a cycle (a page is its own ancestor). None of the pages in the cycle are registered. | Break the cycle in `Parents`/`Children`. |
| NIDDY002 | Error | A class has `[PageRegistration]` but doesn't derive from `Page`. | Derive from `Page` or `Page<TAppDataContext, TPageDataContext>`, or remove the attribute. |
| NIDDY003 | Error | The page can't be created with `new`: abstract, generic (or in a generic type), private or protected, or no public/internal constructor with only optional parameters. | Fix it, or register it by hand with a factory. |
| NIDDY004 | Error | A type listed in `Parents` or `Children` isn't a page. | |
| NIDDY005 | Warning | A listed parent or child has no `[PageRegistration]`. The link only takes effect once that page is registered with `PageRegistry.Register`. | Register it (or add the attribute). |
| NIDDY006 | Error | A `[PageIcon]` is invalid: it doesn't set exactly one of `Data`, `Source` or `ResourceKey`, it has an empty value, a negative or non-finite `Size`, or a relative `Source`. That icon source is skipped. | Use e.g. `Source = "avares://MyApp/Assets/icon.png"`. |
| NIDDY007 | Warning | A class has `[PageIcon]` but no `[PageRegistration]`, so the icon isn't used. | Pass the icon to `PageRegistry.Register` instead. |
| NIDDY008 | Error | `Shortcut` isn't a valid key gesture (empty, an unknown modifier, no key, or an unknown key name). The page isn't registered. | Use modifiers `Primary`, `Ctrl`, `Shift`, `Alt`, `Meta` joined by `+`, then an `Avalonia.Input.Key` name or a single character, e.g. `"Primary+1"`, `"Ctrl+Shift+S"`. |

## Example

```csharp
// NIDDY003: no parameterless constructor
[PageRegistration]
public partial class ReportPage(IReportService reports) : Page<MainDataContext, ReportPageDataContext> { … }

// Fix: register by hand
PageRegistry.Register(() => new ReportPage(services.GetRequiredService<IReportService>()), displayName: "Reports");
```

## See also

- [PageRegistrationGenerator](PageRegistrationGenerator.md), [PageShortcut](../Niddy.Avalonia/PageSystem/PageShortcut.md), [PageIconAttribute](../Niddy.Avalonia/PageSystem/PageIconAttribute.md)
