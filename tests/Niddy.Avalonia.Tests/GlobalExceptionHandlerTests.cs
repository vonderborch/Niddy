using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging;
using Niddy.Avalonia.Hosting;

namespace Niddy.Avalonia.Tests;

[Collection("GlobalExceptionHandler")]
public class GlobalExceptionHandlerTests
{
    private sealed class ListLogger : ILogger, ILoggerProvider
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose()
        {
        }
    }

    [AvaloniaFact]
    public void UIThreadExceptionsAreLoggedRaisedAndKeptRunning()
    {
        var logger = new ListLogger();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logger));
        GlobalExceptionHandler.Install(new GlobalExceptionHandlerOptions { LoggerFactory = factory, ShowDialog = false });
        UnhandledExceptionOccurredEventArgs? raised = null;
        void OnOccurred(object? s, UnhandledExceptionOccurredEventArgs e) => raised = e;
        GlobalExceptionHandler.ExceptionOccurred += OnOccurred;
        try
        {
            var exception = new InvalidOperationException("Boom");
            Assert.True(GlobalExceptionHandler.Handle(exception, UnhandledExceptionSource.UIThread));

            Assert.Same(exception, raised!.Exception);
            Assert.Equal(UnhandledExceptionSource.UIThread, raised.Source);
            Assert.False(raised.ShowDialog);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(exception, entry.Exception);
        }
        finally
        {
            GlobalExceptionHandler.ExceptionOccurred -= OnOccurred;
            GlobalExceptionHandler.Uninstall();
        }
    }

    [AvaloniaFact]
    public void KeepRunningCanBeTurnedOff()
    {
        GlobalExceptionHandler.Install(new GlobalExceptionHandlerOptions { KeepRunning = false, ShowDialog = false });
        try
        {
            Assert.False(GlobalExceptionHandler.Handle(new Exception(), UnhandledExceptionSource.UIThread));
            Assert.True(GlobalExceptionHandler.Handle(new Exception(), UnhandledExceptionSource.UnobservedTask));
        }
        finally
        {
            GlobalExceptionHandler.Uninstall();
        }
    }

    [AvaloniaFact]
    public void DialogsAreOnlyShownForUIThreadExceptionsByDefault()
    {
        GlobalExceptionHandler.Install();
        var shown = new List<(UnhandledExceptionSource, bool)>();
        void OnOccurred(object? s, UnhandledExceptionOccurredEventArgs e)
        {
            shown.Add((e.Source, e.ShowDialog));
            e.Handled = true;
        }
        GlobalExceptionHandler.ExceptionOccurred += OnOccurred;
        try
        {
            GlobalExceptionHandler.Handle(new Exception(), UnhandledExceptionSource.UIThread);
            GlobalExceptionHandler.Handle(new Exception(), UnhandledExceptionSource.UnobservedTask);
            Assert.Equal([(UnhandledExceptionSource.UIThread, true), (UnhandledExceptionSource.UnobservedTask, false)], shown);
        }
        finally
        {
            GlobalExceptionHandler.ExceptionOccurred -= OnOccurred;
            GlobalExceptionHandler.Uninstall();
        }
    }

    [AvaloniaFact]
    public void InstallAndUninstall()
    {
        GlobalExceptionHandler.Install();
        Assert.True(GlobalExceptionHandler.IsInstalled);
        GlobalExceptionHandler.Uninstall();
        Assert.False(GlobalExceptionHandler.IsInstalled);
        Assert.Null(GlobalExceptionHandler.Options);
    }
}
