# PageShortcut

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageShortcut.cs)

Parses page shortcuts: Avalonia key gestures with a platform-neutral `Primary` modifier. `Primary` is Meta (Cmd) on macOS and iOS and Ctrl elsewhere. A lone digit key means that digit key (`"Primary+1"` → `D1`). Everything else follows `KeyGesture.Parse`, e.g. `"Ctrl+Shift+S"` or `"F5"`.

Page shortcuts come from `[PageRegistration(Shortcut = …)]` or `PageRegistry.Register(shortcut: …)`. A [`PageView`](PageView.md) navigates when one of its available pages' shortcuts is pressed in its window (`HandleShortcuts`), and [`PageMenu`](PageMenu.md) shows it in the item tooltip. The generator reports an invalid shortcut as NIDDY008.

## API

| Member | Description |
|---|---|
| `static KeyModifiers PrimaryModifier` | `Meta` on macOS/iOS, `Control` elsewhere. |
| `static KeyGesture Parse(string text)` | Throws `ArgumentException` for an invalid gesture or one with no key. |
| `static bool TryParse(string? text, out KeyGesture? gesture)` | |
| `static string Format(KeyGesture gesture)` | For display in the current culture, e.g. `⌘1` on macOS or `Ctrl+1` elsewhere. |

## Example

```csharp
[PageRegistration(Shortcut = "Primary+1")]   // Cmd+1 on macOS, Ctrl+1 elsewhere
public partial class HomePage : Page;

PageRegistry.Register<SettingsPage>(shortcut: "Ctrl+Shift+S");

// Reuse the convention for your own commands:
var save = PageShortcut.Parse("Primary+S");
window.KeyBindings.Add(new KeyBinding { Gesture = save, Command = SaveCommand });
saveMenuItem.InputGesture = save;
tooltip.Text = $"Save ({PageShortcut.Format(save)})";
```

## See also

- [PageRegistrationAttribute](PageRegistrationAttribute.md), [PageView](PageView.md)
