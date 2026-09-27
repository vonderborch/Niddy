# DialogLayout

`Niddy.Avalonia.Dialogs` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Dialogs/DialogLayout.cs)

The standard dialog layout: a title, a description, the dialog's own content, then a right-aligned row of buttons that wraps on narrow screens. Every built-in dialog inherits it through [`DialogBase<TResult>`](DialogBase.md), and so do custom dialogs. Use it as the AXAML root element. Child content becomes the body, and the title and description are hidden while empty.

## API

| Member | Description |
|---|---|
| `string? Title` | Bold, above the description. Also the window title in window mode. |
| `string? Description` | |
| `IBrush? TitleForeground` | Null uses the normal text color. `Dialog.Warning` uses orange. |
| `Controls Buttons` | Along the bottom, right-aligned. The row is hidden while empty. |

## Example

```xml
<dialogs:DialogLayout xmlns="https://github.com/avaloniaui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:dialogs="clr-namespace:Niddy.Avalonia.Dialogs;assembly=Niddy.Avalonia"
                      x:Class="MyApp.ColorDialog"
                      Title="Pick a colour"
                      TitleForeground="{DynamicResource SystemAccentColor}">
    <ColorPicker Name="Picker" />
    <dialogs:DialogLayout.Buttons>
        <Button Content="OK" IsDefault="True" Click="Ok_OnClick" />
    </dialogs:DialogLayout.Buttons>
</dialogs:DialogLayout>
```

Buttons can also be added in code:

```csharp
var retry = new Button { Content = "Retry" };
retry.Click += (_, _) => Close(true);
Buttons.Insert(0, retry);
```

## See also

- [DialogBase](DialogBase.md)
