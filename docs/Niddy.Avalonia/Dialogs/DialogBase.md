# DialogBase&lt;TResult&gt;

`Niddy.Avalonia.Dialogs` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Dialogs/DialogBase.cs)

The base class for custom dialogs shown with [`Dialog.Show`](Dialog.md). A dialog is just its content. The same control is shown in an overlay card or in a window, and the host provides the chrome. It inherits the standard title, description and button row from [`DialogLayout`](DialogLayout.md), so use `DialogLayout` as the root element of the dialog's AXAML.

Call `Close(result)` from a button. The user can also dismiss the dialog without a button (Escape, the window close button, platform back). `CanDismiss` and `DismissResult` control what happens then.

## API

| Member | Description |
|---|---|
| `Task<TResult> Result` | Completes when the dialog closes. |
| `string? WindowTitle` | The window's title. Defaults to `Title`. Ignored in overlays. |
| `protected abstract TResult DismissResult` | The result when dismissed without a button. |
| `protected virtual bool CanDismiss` | Default true. Return false when there's no visible cancel button. |
| `protected virtual Control? InitialFocus` | The control to focus when shown. Defaults to the dialog itself. |
| `protected void Close(TResult result)` | Safe from any thread. Later calls are ignored. |
| `protected virtual void OnDismissed()` | Called just before closing with `DismissResult`, e.g. to cancel work. |

## Example

`RenameDialog.axaml`:

```xml
<dialogs:DialogLayout xmlns="https://github.com/avaloniaui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:dialogs="clr-namespace:Niddy.Avalonia.Dialogs;assembly=Niddy.Avalonia"
                      x:Class="MyApp.RenameDialog"
                      Title="Rename" Description="Choose a new name.">
    <StackPanel Spacing="8">
        <TextBox Name="NameBox" />
        <CheckBox Name="KeepCopy" Content="Keep a copy" />
    </StackPanel>
    <dialogs:DialogLayout.Buttons>
        <Button Content="Cancel" IsCancel="True" Click="Cancel_OnClick" />
        <Button Content="Rename" IsDefault="True" Classes="accent" Click="Ok_OnClick" />
    </dialogs:DialogLayout.Buttons>
</dialogs:DialogLayout>
```

`RenameDialog.axaml.cs`:

```csharp
public record RenameResult(string Name, bool KeepCopy);

public partial class RenameDialog : DialogBase<RenameResult?>
{
    public RenameDialog(string current)
    {
        InitializeComponent();
        NameBox.Text = current;
    }

    protected override RenameResult? DismissResult => null;
    protected override Control InitialFocus => NameBox;

    private void Ok_OnClick(object? sender, RoutedEventArgs e) => Close(new(NameBox.Text ?? "", KeepCopy.IsChecked == true));
    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}

// Usage
RenameResult? result = await Dialog.Show(this, new RenameDialog(file.Name), maxWidth: 420);
```

## See also

- [Dialog](Dialog.md), [DialogLayout](DialogLayout.md), [examples/custom-dialog](../../examples/custom-dialog.md)
