using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Niddy.Avalonia.Dialogs;

/// <summary>
///     Runs background work with a progress bar. The result is true if the work completed, false if it
///     was cancelled or faulted.
/// </summary>
internal sealed partial class ProgressDialog : DialogBase<bool>
{
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    protected override bool CanDismiss => ButtonCancel.IsVisible;

    protected override bool DismissResult => false;

    public ProgressDialog()
    {
        InitializeComponent();
    }

    internal ProgressDialog(string title, string description, bool isIndeterminate, bool showCancelButton, string cancelButtonText)
        : this()
    {
        Title = title;
        Description = description;
        ProgressBarMain.IsIndeterminate = isIndeterminate;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = showCancelButton;
    }

    protected override void OnDismissed()
    {
        Cancel();
    }

    /// <summary>
    ///     Starts <paramref name="work" /> on a background thread and closes the dialog when it finishes.
    ///     Call on the UI thread. The returned task completes when the work has finished (it never throws).
    /// </summary>
    internal Task Start(Func<IProgress<double>, CancellationToken, Task> work)
    {
        Progress<double> progress = new Progress<double>(value =>
        {
            ProgressBarMain.Value = Math.Clamp(value, 0.0, 100.0);
        });
        CancellationToken token = _cts.Token;
        Task task = Task.Run(() => work(progress, token), token);
        return task.ContinueWith(t =>
        {
            Close(t.IsCompletedSuccessfully && !token.IsCancellationRequested);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(_cts.Dispose);
        }, TaskScheduler.Default);
    }

    /// <summary>Cancels the work, e.g. after the dialog was closed without the work finishing.</summary>
    internal void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Cancel();
        Close(result: false);
    }
}
