# A custom dialog

Built-in dialogs cover most needs (see [Dialog](../Niddy.Avalonia/Dialogs/Dialog.md)). When one doesn't fit, subclass [`DialogBase<TResult>`](../Niddy.Avalonia/Dialogs/DialogBase.md): it's a [`DialogLayout`](../Niddy.Avalonia/Dialogs/DialogLayout.md), so you get the standard title, description and button row, and the same control works as an overlay or a window on every platform.

This example is a "Connect to server" dialog. It validates its input, tests the connection before closing, and cancels the test if the user dismisses it.

`ConnectDialog.axaml`:

```xml
<dialogs:DialogLayout xmlns="https://github.com/avaloniaui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:dialogs="clr-namespace:Niddy.Avalonia.Dialogs;assembly=Niddy.Avalonia"
                      x:Class="MyApp.ConnectDialog"
                      Title="Connect to server"
                      Description="Enter the server's address. The connection is tested before it's saved.">
    <StackPanel Spacing="8">
        <TextBox Name="AddressBox" Watermark="https://server.example.com" TextChanged="Address_OnTextChanged" />
        <TextBox Name="UserBox" Watermark="User name" />
        <StackPanel Orientation="Horizontal" Spacing="8">
            <ProgressBar Name="Testing" IsIndeterminate="True" Width="80" IsVisible="False" />
            <TextBlock Name="ErrorText" Foreground="{DynamicResource SystemFillColorCriticalBrush}" TextWrapping="Wrap" />
        </StackPanel>
    </StackPanel>
    <dialogs:DialogLayout.Buttons>
        <Button Content="Cancel" IsCancel="True" Click="Cancel_OnClick" />
        <Button Name="ConnectButton" Content="Connect" IsDefault="True" Classes="accent" IsEnabled="False"
                Click="Connect_OnClick" />
    </dialogs:DialogLayout.Buttons>
</dialogs:DialogLayout>
```

`ConnectDialog.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Niddy.Avalonia.Dialogs;

namespace MyApp;

public sealed record ServerConnection(Uri Address, string User);

public partial class ConnectDialog : DialogBase<ServerConnection?>
{
    private readonly IServerClient _client;
    private CancellationTokenSource? _test;

    public ConnectDialog(IServerClient client, ServerConnection? current = null)
    {
        _client = client;
        InitializeComponent();
        AddressBox.Text = current?.Address.ToString();
        UserBox.Text = current?.User;
    }

    // Escape, the window's close button or platform back: no connection
    protected override ServerConnection? DismissResult => null;
    protected override Control InitialFocus => AddressBox;

    // Dismissed mid-test: stop testing
    protected override void OnDismissed() => _test?.Cancel();

    private void Address_OnTextChanged(object? sender, TextChangedEventArgs e) =>
        ConnectButton.IsEnabled = Uri.TryCreate(AddressBox.Text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private async void Connect_OnClick(object? sender, RoutedEventArgs e)
    {
        var connection = new ServerConnection(new Uri(AddressBox.Text!), UserBox.Text ?? "");
        _test = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ConnectButton.IsEnabled = false;
        Testing.IsVisible = true;
        ErrorText.Text = null;
        try
        {
            await _client.PingAsync(connection.Address, _test.Token);
            Close(connection);
        }
        catch (Exception ex)
        {
            // Also runs after a dismissal cancels the test; the dialog is closed by then, so this is harmless
            ErrorText.Text = ex is OperationCanceledException ? "The server didn't answer." : ex.Message;
        }
        finally
        {
            Testing.IsVisible = false;
            ConnectButton.IsEnabled = true;
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
```

Showing it from a page:

```csharp
ServerConnection? connection = await Dialog.Show(this, new ConnectDialog(_client, Settings.Current.Server), maxWidth: 460);
if (connection is not null)
    Settings.Update(s => s.Server = connection);
```

## Notes

- **Result type.** Make it nullable (or a record with a "cancelled" state) so `DismissResult` has something to return.
- **`Close` is final.** Only the first call counts, so a slow test finishing after Cancel can't overwrite the result. `Close` is safe from any thread.
- **`CanDismiss`.** Override it to return false when there's no cancel button, so Escape and back can't close the dialog without an answer.
- **Sizing.** `maxWidth` caps the overlay card and sets the window's width. Use `windowWidth`/`windowHeight` for a fixed window size. Force a mode per call with `displayMode:`.
- **No logic in the parent.** The page only gets back a `ServerConnection?`, which makes the dialog easy to reuse and to test headlessly (`new ConnectDialog(fake)`, click, then await `dialog.Result`).

## See also

- [DialogOverlayHost](../Niddy.Avalonia/Dialogs/DialogOverlayHost.md), [long-running-work](long-running-work.md)
