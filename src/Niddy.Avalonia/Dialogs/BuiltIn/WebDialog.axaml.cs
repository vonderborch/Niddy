using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Niddy.Avalonia.Dialogs.BuiltIn;

/// <summary>
/// A dialog showing a web page or HTML in the platform's web view. The result is the address shown when it closed
/// (null for HTML that never navigated), or the address that matched <c>closeWhen</c>.
/// </summary>
internal sealed partial class WebDialog : DialogBase<Uri?>
{
    private readonly Func<Uri, bool>? _closeWhen;

    private NativeWebView? _webView;

    /// <summary>The address last shown, or null for HTML that hasn't navigated anywhere.</summary>
    internal Uri? CurrentAddress { get; private set; }

    protected override Uri? DismissResult => CurrentAddress;

    public WebDialog()
    {
        InitializeComponent();
    }

    internal WebDialog(string title, Uri? source, string? html, double height, Func<Uri, bool>? closeWhen, string closeButtonText, string? openInBrowserText)
        : this()
    {
        Title = title;
        _closeWhen = closeWhen;
        CurrentAddress = source;
        PanelWebHost.Height = height;
        ButtonClose.Content = closeButtonText;
        ButtonOpenInBrowser.Content = openInBrowserText;
        UpdateOpenInBrowser(openInBrowserText != null);
        AttachedToVisualTree += (_, _) =>
        {
            if (_webView != null || PanelWebHost.Children.Count > 0)
            {
                return;
            }

            try
            {
                _webView = new NativeWebView();
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or InvalidOperationException or NotSupportedException)
            {
                PanelWebHost.Children.Add(new TextBlock
                {
                    Text = $"Web content can't be shown on this platform.{Environment.NewLine}{source}",
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            _webView.NavigationStarted += (_, e) =>
            {
                if (e.Request is { } request && OnNavigating(request))
                {
                    e.Cancel = true;
                }
            };
            _webView.NavigationCompleted += (_, e) =>
            {
                if (e.Request is { Scheme: "http" or "https" or "file" } request)
                {
                    CurrentAddress = request;
                }

                UpdateOpenInBrowser(openInBrowserText != null);
            };
            PanelWebHost.Children.Add(_webView);
            if (html != null)
            {
                _webView.NavigateToString(html, source ?? new Uri("about:blank"));
            }
            else if (source != null)
            {
                _webView.Navigate(source);
            }
        };
    }

    /// <summary>Closes the dialog if <paramref name="request" /> matches <c>closeWhen</c>; returns whether it did.</summary>
    internal bool OnNavigating(Uri request)
    {
        if (_closeWhen == null || !_closeWhen(request))
        {
            return false;
        }

        CurrentAddress = request;
        Close(request);
        return true;
    }

    private void UpdateOpenInBrowser(bool enabled) =>
        ButtonOpenInBrowser.IsVisible = enabled && CurrentAddress is { Scheme: "http" or "https" };

    private async void ButtonOpenInBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentAddress is { } address)
        {
            await UriLauncher.TryLaunchAsync(this, address.ToString());
        }
    }

    private void ButtonClose_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(CurrentAddress);
    }
}
