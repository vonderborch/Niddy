using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Niddy.Avalonia.Dialogs;
using AvaloniaApplication = Avalonia.Application;

namespace Niddy.Avalonia.Hosting;

/// <summary>Where an unhandled exception was caught.</summary>
public enum UnhandledExceptionSource
{
    /// <summary>Thrown on the UI thread, e.g. by an event handler or <c>async void</c> method. The app can keep running.</summary>
    UIThread,

    /// <summary>A faulted task that was never awaited, found when it was garbage collected. The app keeps running.</summary>
    UnobservedTask,

    /// <summary>Thrown on a background thread. The runtime ends the process after this.</summary>
    AppDomain,
}

/// <summary>An unhandled exception, raised by <see cref="GlobalExceptionHandler.ExceptionOccurred"/>.</summary>
public sealed class UnhandledExceptionOccurredEventArgs(Exception exception, UnhandledExceptionSource source, bool showDialog) : EventArgs
{
    /// <summary>The exception.</summary>
    public Exception Exception { get; } = exception;

    /// <summary>Where it was caught.</summary>
    public UnhandledExceptionSource Source { get; } = source;

    /// <summary>Whether the process ends after this, as it does after <see cref="UnhandledExceptionSource.AppDomain"/> exceptions.</summary>
    public bool IsTerminating => Source == UnhandledExceptionSource.AppDomain;

    /// <summary>
    ///     Whether to show the exception dialog. Starts as the handler's setting for this source; set it to false to
    ///     show your own UI instead. Ignored for <see cref="UnhandledExceptionSource.AppDomain"/>.
    /// </summary>
    public bool ShowDialog { get; set; } = showDialog;

    /// <summary>
    ///     Set to true to suppress the default handling: the dialog and, for UI-thread exceptions, keeping the app
    ///     running (it's still logged). Leave false to let the handler deal with it.
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>Configures <see cref="GlobalExceptionHandler"/>.</summary>
public sealed class GlobalExceptionHandlerOptions
{
    /// <summary>Where exceptions are logged. Defaults to nothing.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    ///     Whether exceptions on the UI thread are swallowed so the app keeps running, instead of crashing it.
    ///     Defaults to true.
    /// </summary>
    public bool KeepRunning { get; set; } = true;

    /// <summary>Whether UI-thread exceptions show <see cref="Dialog.Exception"/>. Defaults to true.</summary>
    public bool ShowDialog { get; set; } = true;

    /// <summary>Whether unobserved task exceptions show the dialog too. Defaults to false; they're only logged.</summary>
    public bool ShowDialogForUnobservedTasks { get; set; }

    /// <summary>The dialog's title.</summary>
    public string DialogTitle { get; set; } = "Something went wrong";

    /// <summary>The dialog's description.</summary>
    public string DialogDescription { get; set; } = "An unexpected error occurred. You can continue, but the app may not behave correctly.";

    /// <summary>The URL the dialog's Report Issue button opens. The button is hidden when this is empty.</summary>
    public string ReportIssueLink { get; set; } = string.Empty;
}

/// <summary>
///     Catches exceptions nothing else did — on the UI thread, in unobserved tasks and on background threads — logs
///     them, raises <see cref="ExceptionOccurred"/>, and shows <see cref="Dialog.Exception"/> for UI-thread ones
///     instead of letting the app crash. <see cref="NiddyApp"/> installs it unless
///     <see cref="NiddyAppOptions.HandleUnhandledExceptions"/> is false; otherwise call <see cref="Install"/> once the
///     UI thread is running.
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly Lock Sync = new();
    private static GlobalExceptionHandlerOptions? _options;
    private static ILogger _logger = NullLogger.Instance;
    private static bool _dialogOpen;

    /// <summary>Whether the handler is installed.</summary>
    public static bool IsInstalled => _options is not null;

    /// <summary>The installed options, or null.</summary>
    public static GlobalExceptionHandlerOptions? Options => _options;

    /// <summary>
    ///     Raised for every unhandled exception before the default handling. For UI-thread exceptions it's raised on
    ///     the UI thread; otherwise on whatever thread found it.
    /// </summary>
    public static event EventHandler<UnhandledExceptionOccurredEventArgs>? ExceptionOccurred;

    /// <summary>Starts catching unhandled exceptions, replacing any earlier installation.</summary>
    public static void Install(GlobalExceptionHandlerOptions? options = null)
    {
        lock (Sync)
        {
            Uninstall();
            _options = options ?? new GlobalExceptionHandlerOptions();
            _logger = _options.LoggerFactory?.CreateLogger("Niddy.UnhandledException") ?? NullLogger.Instance;
            Dispatcher.UIThread.UnhandledException += OnDispatcherException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
        }
    }

    /// <summary>Stops catching unhandled exceptions.</summary>
    public static void Uninstall()
    {
        lock (Sync)
        {
            if (_options is null)
                return;
            Dispatcher.UIThread.UnhandledException -= OnDispatcherException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainException;
            _options = null;
            _logger = NullLogger.Instance;
        }
    }

    /// <summary>
    ///     Handles an exception as if it were unhandled from <paramref name="source"/>: logs it, raises
    ///     <see cref="ExceptionOccurred"/> and shows the dialog as configured. Use it for exceptions you catch
    ///     yourself but want reported the same way, e.g. in a fire-and-forget task.
    /// </summary>
    /// <returns>Whether the app should keep running (always false for <see cref="UnhandledExceptionSource.AppDomain"/>).</returns>
    public static bool Report(Exception exception, UnhandledExceptionSource source = UnhandledExceptionSource.UIThread) =>
        Handle(exception, source);

    internal static bool Handle(Exception exception, UnhandledExceptionSource source)
    {
        var options = _options ?? new GlobalExceptionHandlerOptions();
        var logger = _logger;

        logger.Log(
            source == UnhandledExceptionSource.AppDomain ? LogLevel.Critical : LogLevel.Error,
            exception,
            "Unhandled exception ({Source})",
            source);

        var args = new UnhandledExceptionOccurredEventArgs(exception, source, source switch
        {
            UnhandledExceptionSource.UIThread => options.ShowDialog,
            UnhandledExceptionSource.UnobservedTask => options.ShowDialogForUnobservedTasks,
            _ => false,
        });

        try
        {
            ExceptionOccurred?.Invoke(null, args);
        }
        catch (Exception handlerException)
        {
            logger.LogError(handlerException, "An ExceptionOccurred handler threw");
        }

        if (source == UnhandledExceptionSource.AppDomain)
        {
            // The process is about to end: make sure the log reaches disk.
            options.LoggerFactory?.Dispose();
            return false;
        }

        if (args.Handled)
            return source != UnhandledExceptionSource.UIThread || options.KeepRunning;

        if (args.ShowDialog)
        {
            if (Dispatcher.UIThread.CheckAccess())
                ShowDialog(exception, options);
            else
                Dispatcher.UIThread.Post(() => ShowDialog(exception, options));
        }

        return source != UnhandledExceptionSource.UIThread || options.KeepRunning;
    }

    private static void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (Handle(e.Exception, UnhandledExceptionSource.UIThread))
            e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception.InnerExceptions.Count == 1 ? e.Exception.InnerExceptions[0] : e.Exception;
        Handle(exception, UnhandledExceptionSource.UnobservedTask);
        e.SetObserved();
    }

    private static void OnAppDomainException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            Handle(exception, UnhandledExceptionSource.AppDomain);
    }

    private static async void ShowDialog(Exception exception, GlobalExceptionHandlerOptions options)
    {
        // A failing timer or render callback would otherwise stack up one dialog per tick.
        if (_dialogOpen || FindParent() is not { } parent)
            return;

        _dialogOpen = true;
        try
        {
            await Dialog.Exception.Open(
                parent,
                options.DialogTitle,
                options.DialogDescription,
                exception,
                options.ReportIssueLink,
                showExitProgramButton: null);
        }
        catch (Exception dialogException)
        {
            _logger.LogError(dialogException, "Couldn't show the exception dialog");
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private static Control? FindParent() => AvaloniaApplication.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop =>
            desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow,
        ISingleViewApplicationLifetime singleView => singleView.MainView,
        _ => null,
    };
}
