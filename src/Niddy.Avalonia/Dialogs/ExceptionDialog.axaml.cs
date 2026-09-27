using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Niddy.Avalonia.Utilities;

namespace Niddy.Avalonia.Dialogs;

/// <summary>Shows exception details with Report Issue, Ignore and Continue, and Exit Program buttons.</summary>
internal sealed partial class ExceptionDialog : DialogBase<bool>
{
    private readonly string _reportIssueLink = string.Empty;

    private readonly string _details = string.Empty;

    protected override bool CanDismiss => ButtonIgnoreAndContinue.IsVisible;

    protected override bool DismissResult => false;

    /// <summary>The full exception text copied by Copy Details, including inner exceptions.</summary>
    internal string Details => _details;

    public ExceptionDialog()
    {
        InitializeComponent();
    }

    internal ExceptionDialog(string title, string description, Exception exception, string reportIssueLink, string reportIssueText, string ignoreAndContinueText, string exitProgramText, bool showReportIssueButton, bool showIgnoreAndContinueButton, bool? showExitProgramButton, string? copyDetailsText = "Copy Details")
        : this()
    {
        _details = exception.ToString();
        _reportIssueLink = reportIssueLink;
        Title = title;
        Description = description;
        TextBlockExceptionDetails.Text = ((exception.HelpLink != null) ? $"{exception.HelpLink}{Environment.NewLine}{exception.Message}{Environment.NewLine}{exception.StackTrace}" : (exception.Message + Environment.NewLine + exception.StackTrace));
        ButtonCopyDetails.Content = copyDetailsText;
        ButtonCopyDetails.IsVisible = copyDetailsText != null;
        ButtonReportIssue.Content = reportIssueText;
        ButtonIgnoreAndContinue.Content = ignoreAndContinueText;
        ButtonExitProgram.Content = exitProgramText;
        ButtonReportIssue.IsVisible = showReportIssueButton && !string.IsNullOrWhiteSpace(reportIssueLink);
        ButtonIgnoreAndContinue.IsVisible = showIgnoreAndContinueButton;
        IApplicationLifetime applicationLifetime = Application.Current?.ApplicationLifetime;
        ButtonExitProgram.IsVisible = (showExitProgramButton ?? (applicationLifetime is IClassicDesktopStyleApplicationLifetime)) && applicationLifetime is IControlledApplicationLifetime;
    }

    private async void ButtonCopyDetails_OnClick(object? sender, RoutedEventArgs e)
    {
        IClipboard clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(_details);
        }
    }

    private async void ButtonReportIssue_OnClick(object? sender, RoutedEventArgs e)
    {
        await UriLauncher.TryLaunchAsync(this, _reportIssueLink);
    }

    private void ButtonExitProgram_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(result: true);
        if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime controlledApplicationLifetime)
        {
            controlledApplicationLifetime.Shutdown(1);
        }
    }

    private void ButtonIgnoreAndContinue_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(result: false);
    }
}
