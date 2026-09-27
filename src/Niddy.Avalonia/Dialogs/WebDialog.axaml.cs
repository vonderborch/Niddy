using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Niddy.Avalonia.Utilities;

namespace Niddy.Avalonia.Dialogs;

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
        WebDialog webDialog = this;
        Title = title;
        _closeWhen = closeWhen;
        CurrentAddress = source;
        PanelWebHost.Height = height;
        ButtonClose.Content = closeButtonText;
        ButtonOpenInBrowser.Content = openInBrowserText;
        UpdateOpenInBrowser(openInBrowserText != null);
        AttachedToVisualTree += (_, _) =>
        {
            if (webDialog._webView == null && webDialog.PanelWebHost.Children.Count <= 0)
            {
                try
                {
                    webDialog._webView = new NativeWebView();
                }
                catch (Exception ex) when (((ex is PlatformNotSupportedException || ex is InvalidOperationException || ex is NotSupportedException) ? 1 : 0) != 0)
                {
                    webDialog.PanelWebHost.Children.Add(new TextBlock
                    {
                        Text = $"Web content can't be shown on this platform.{Environment.NewLine}{source}",
                        TextWrapping = TextWrapping.Wrap
                    });
                    return;
                }
                webDialog._webView.NavigationStarted += (obj, e) =>
                {
                    Uri request = e.Request;
                    if ((object)request != null && webDialog.OnNavigating(request))
                    {
                        e.Cancel = true;
                    }
                };
                webDialog._webView.NavigationCompleted += (obj, e) =>
                {
                    Uri request = e.Request;
                    bool flag = (object)request != null;
                    bool flag2 = flag;
                    if (flag2)
                    {
                        bool flag3;
                        switch (request.Scheme)
                        {
                        case "http":
                        case "https":
                        case "file":
                            flag3 = true;
                            break;
                        default:
                            flag3 = false;
                            break;
                        }
                        flag2 = flag3;
                    }
                    if (flag2)
                    {
                        webDialog.CurrentAddress = request;
                    }
                    webDialog.UpdateOpenInBrowser(openInBrowserText != null);
                };
                webDialog.PanelWebHost.Children.Add(webDialog._webView);
                if (html != null)
                {
                    webDialog._webView.NavigateToString(html, source ?? new Uri("about:blank"));
                }
                else if ((object)source != null)
                {
                    webDialog._webView.Navigate(source);
                }
            }
        };
    }

    /// <summary>Closes the dialog if <paramref name="request" /> matches <c>closeWhen</c>; returns whether it did.</summary>
    internal bool OnNavigating(Uri request)
    {
        Func<Uri, bool>? closeWhen = _closeWhen;
        if (closeWhen == null || !closeWhen(request))
        {
            return false;
        }
        CurrentAddress = request;
        Close(request);
        return true;
    }

    private void UpdateOpenInBrowser(bool enabled)
    {
        Button buttonOpenInBrowser = ButtonOpenInBrowser;
        bool flag = enabled;
        bool flag2 = flag;
        bool flag3;
        if (flag2)
        {
            Uri currentAddress = CurrentAddress;
            if ((object)currentAddress != null)
            {
                string scheme = currentAddress.Scheme;
                if (scheme == "http" || scheme == "https")
                {
                    flag3 = true;
                    goto IL_0046;
                }
            }
            flag3 = false;
            goto IL_0046;
        }
        goto IL_0049;
        IL_0049:
        buttonOpenInBrowser.IsVisible = flag2;
        return;
        IL_0046:
        flag2 = flag3;
        goto IL_0049;
    }

    private async void ButtonOpenInBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        Uri address = CurrentAddress;
        if ((object)address != null)
        {
            await UriLauncher.TryLaunchAsync(this, address.ToString());
        }
    }

    private void ButtonClose_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(CurrentAddress);
    }
}
