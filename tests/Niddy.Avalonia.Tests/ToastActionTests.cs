using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Niddy.Avalonia.Toast;

namespace Niddy.Avalonia.Tests;

public class ToastActionTests
{
    private static (ToastHost Host, Border Inner) Show()
    {
        var inner = new Border();
        var host = new ToastHost { Content = inner };
        new Window { Width = 800, Height = 600, Content = host }.Show();
        Dispatcher.UIThread.RunJobs();
        return (host, inner);
    }

    [AvaloniaFact]
    public void ActionButtonRunsTheActionAndDismisses()
    {
        var (host, inner) = Show();
        var undone = 0;

        var handle = Toast.Toast.Show(inner, "Deleted", actionText: "Undo", action: () => undone++);
        var item = Assert.Single(host.Toasts);
        Assert.Equal("Undo", item.ActionText);
        Assert.Equal(Toast.Toast.DefaultActionDuration, item.Duration);

        item.ActionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, undone);
        Assert.True(handle.IsDismissed);
        Assert.True(handle.Dismissed.IsCompleted);
        Assert.Empty(host.Toasts);
    }

    [AvaloniaFact]
    public void DuplicatesAreMerged()
    {
        var (host, inner) = Show();

        var first = Toast.Toast.Show(inner, "Saved", ToastType.Success);
        var second = Toast.Toast.Show(inner, "Saved", ToastType.Success);
        Toast.Toast.Show(inner, "Saved", ToastType.Info);

        Assert.Same(first, second);
        Assert.Equal(2, host.Toasts.Count);
        Assert.Equal(2, first.Item.Count);
        Assert.True(first.Item.CountBadge.IsVisible);
    }

    [AvaloniaFact]
    public void KeysMergeDifferentMessages()
    {
        var (host, inner) = Show();

        var first = Toast.Toast.Show(inner, "Syncing 1", key: "sync");
        var second = Toast.Toast.Show(inner, "Syncing 2", key: "sync");

        Assert.Same(first, second);
        Assert.Equal("Syncing 2", Assert.Single(host.Toasts).Message);
    }

    [AvaloniaFact]
    public void MergingCanBeTurnedOff()
    {
        var (host, inner) = Show();
        host.MergeDuplicates = false;

        Toast.Toast.Show(inner, "Saved");
        Toast.Toast.Show(inner, "Saved");

        Assert.Equal(2, host.Toasts.Count);
    }

    [AvaloniaFact]
    public void DismissedToastsAreNotMergedInto()
    {
        var (host, inner) = Show();

        var first = Toast.Toast.Show(inner, "Saved");
        first.Dismiss();
        var second = Toast.Toast.Show(inner, "Saved");

        Assert.NotSame(first, second);
        Assert.Single(host.Toasts);
    }

    [AvaloniaFact]
    public void ProgressToastsReportAndComplete()
    {
        var (host, inner) = Show();

        var handle = Toast.Toast.ShowProgress(inner, "Exporting…");
        var item = Assert.Single(host.Toasts);
        Assert.True(double.IsNaN(item.Progress!.Value));
        Assert.Equal(TimeSpan.Zero, item.Duration);

        handle.Report(0.5);
        Assert.Equal(0.5, item.Progress);

        handle.Complete("Exported", duration: TimeSpan.FromSeconds(1));
        Assert.Null(item.Progress);
        Assert.Equal("Exported", item.Message);
        Assert.Equal(ToastType.Success, item.Type);
        Assert.Equal(TimeSpan.FromSeconds(1), item.Duration);
        Assert.False(handle.IsDismissed);
    }

    [AvaloniaFact]
    public void ProgressToastsAreNeverMerged()
    {
        var (host, inner) = Show();

        Toast.Toast.ShowProgress(inner, "Working");
        Toast.Toast.ShowProgress(inner, "Working");

        Assert.Equal(2, host.Toasts.Count);
    }

    [AvaloniaFact]
    public void ProgressCancelRunsTheCallback()
    {
        var (host, inner) = Show();
        var cancelled = false;

        var handle = Toast.Toast.ShowProgress(inner, "Working", cancelText: "Cancel", cancel: () => cancelled = true);
        Assert.Single(host.Toasts).InvokeAction();

        Assert.True(cancelled);
        Assert.True(handle.IsDismissed);
    }

    [AvaloniaFact]
    public void UpdateChangesTheMessageAndType()
    {
        var (host, inner) = Show();

        var handle = Toast.Toast.Show(inner, "Uploading");
        handle.Update("Upload failed", ToastType.Error);

        var item = Assert.Single(host.Toasts);
        Assert.Equal("Upload failed", item.Message);
        Assert.Equal(ToastType.Error, item.Type);
    }
}
