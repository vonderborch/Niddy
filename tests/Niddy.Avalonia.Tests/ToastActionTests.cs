using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Niddy.Avalonia.Toasts;

namespace Niddy.Avalonia.Tests;

public class ToastActionTests
{
    private static (ToastHost Host, Border Inner) Show()
    {
        Border inner = new();
        ToastHost host = new() { Content = inner };
        new Window { Width = 800, Height = 600, Content = host }.Show();
        Dispatcher.UIThread.RunJobs();
        return (host, inner);
    }

    [AvaloniaFact]
    public void ActionButtonRunsTheActionAndDismisses()
    {
        (ToastHost host, Border inner) = Show();
        int undone = 0;

        ToastHandle handle = Toast.Show(inner, "Deleted", actionText: "Undo", action: () => undone++);
        ToastItem item = Assert.Single(host.Toasts);
        Assert.Equal("Undo", item.ActionText);
        Assert.Equal(Toast.DefaultActionDuration, item.Duration);

        item.ActionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, undone);
        Assert.True(handle.IsDismissed);
        Assert.True(handle.Dismissed.IsCompleted);
        Assert.Empty(host.Toasts);
    }

    [AvaloniaFact]
    public void DuplicatesAreMerged()
    {
        (ToastHost host, Border inner) = Show();

        ToastHandle first = Toast.Show(inner, "Saved", ToastType.Success);
        ToastHandle second = Toast.Show(inner, "Saved", ToastType.Success);
        Toast.Show(inner, "Saved", ToastType.Info);

        Assert.Same(first, second);
        Assert.Equal(2, host.Toasts.Count);
        Assert.Equal(2, first.Item.Count);
        Assert.True(first.Item.CountBadge.IsVisible);
    }

    [AvaloniaFact]
    public void KeysMergeDifferentMessages()
    {
        (ToastHost host, Border inner) = Show();

        ToastHandle first = Toast.Show(inner, "Syncing 1", key: "sync");
        ToastHandle second = Toast.Show(inner, "Syncing 2", key: "sync");

        Assert.Same(first, second);
        Assert.Equal("Syncing 2", Assert.Single(host.Toasts).Message);
    }

    [AvaloniaFact]
    public void MergingCanBeTurnedOff()
    {
        (ToastHost host, Border inner) = Show();
        host.MergeDuplicates = false;

        Toast.Show(inner, "Saved");
        Toast.Show(inner, "Saved");

        Assert.Equal(2, host.Toasts.Count);
    }

    [AvaloniaFact]
    public void DismissedToastsAreNotMergedInto()
    {
        (ToastHost host, Border inner) = Show();

        ToastHandle first = Toast.Show(inner, "Saved");
        first.Dismiss();
        ToastHandle second = Toast.Show(inner, "Saved");

        Assert.NotSame(first, second);
        Assert.Single(host.Toasts);
    }

    [AvaloniaFact]
    public void ProgressToastsReportAndComplete()
    {
        (ToastHost host, Border inner) = Show();

        ToastHandle handle = Toast.ShowProgress(inner, "Exporting…");
        ToastItem item = Assert.Single(host.Toasts);
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
        (ToastHost host, Border inner) = Show();

        Toast.ShowProgress(inner, "Working");
        Toast.ShowProgress(inner, "Working");

        Assert.Equal(2, host.Toasts.Count);
    }

    [AvaloniaFact]
    public void ProgressCancelRunsTheCallback()
    {
        (ToastHost host, Border inner) = Show();
        bool cancelled = false;

        ToastHandle handle = Toast.ShowProgress(inner, "Working", cancelText: "Cancel", cancel: () => cancelled = true);
        Assert.Single(host.Toasts).InvokeAction();

        Assert.True(cancelled);
        Assert.True(handle.IsDismissed);
    }

    [AvaloniaFact]
    public void UpdateChangesTheMessageAndType()
    {
        (ToastHost host, Border inner) = Show();

        ToastHandle handle = Toast.Show(inner, "Uploading");
        handle.Update("Upload failed", ToastType.Error);

        ToastItem item = Assert.Single(host.Toasts);
        Assert.Equal("Upload failed", item.Message);
        Assert.Equal(ToastType.Error, item.Type);
    }
}
