# OAuth sign-in

Signing in with an OAuth provider inside the app, then keeping the token safe. It uses [Dialog.Web](../Niddy.Avalonia/Dialogs/Dialog.md), [ISecureStorage](../Niddy.Core/Security/ISecureStorage.md), [SecureStorageFactory](../Niddy.Core/Security/SecureStorageFactory.md), [Toast](../Niddy.Avalonia/Toast/Toast.md) and [DataContextBase](../Niddy.Avalonia/DataContexts/DataContextBase.md).

`Dialog.Web` shows the provider's page in the platform web view. Its `closeWhen` callback sees every navigation. When the provider redirects to your callback URL, the dialog cancels that navigation, closes, and returns the URL with the code in it.

```csharp
using System.Web;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Toast;
using Niddy.Security;

public sealed class GitHubSignIn(ISecureStorage secrets, HttpClient http)
{
    private const string ClientId = "your-client-id";
    private const string Callback = "http://127.0.0.1/callback";   // registered with the provider; never actually loaded

    public string? Token => secrets.GetToken("github-token");

    public async Task<bool> SignInAsync(Control parent)
    {
        var state = Guid.NewGuid().ToString("N");
        var authorize = new Uri(
            $"https://github.com/login/oauth/authorize?client_id={ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(Callback)}&scope=repo&state={state}");

        Uri? result = await Dialog.Web.Open(parent, "Sign in to GitHub", authorize,
            closeWhen: uri => uri.AbsoluteUri.StartsWith(Callback, StringComparison.OrdinalIgnoreCase),
            openInBrowserText: null,                         // keep the flow in the app
            displayMode: DialogDisplayMode.Window);          // web dialogs work best as windows; falls back to an overlay on mobile

        // Closing the dialog early returns the last address shown, so check it's the callback
        if (result is null || !result.AbsoluteUri.StartsWith(Callback, StringComparison.OrdinalIgnoreCase))
            return false;

        var query = HttpUtility.ParseQueryString(result.Query);
        if (query["state"] != state || query["code"] is not { } code)
            return false;

        var token = await ExchangeCodeAsync(code);           // your back end or the provider's token endpoint
        secrets.SetToken("github-token", token);
        return true;
    }

    public void SignOut() => secrets.SetToken("github-token", null);

    private Task<string> ExchangeCodeAsync(string code) => throw new NotImplementedException();
}
```

Using it from a page:

```csharp
private async void SignIn_OnClick(object? sender, RoutedEventArgs e)
{
    if (await _signIn.SignInAsync(this))
        Toast.Show(this, "Signed in", ToastType.Success);
    else
        Toast.Show(this, "Sign-in cancelled", ToastType.Warning);
}
```

Create the storage once, e.g. in the app data context: `SecureStorageFactory.Create(NiddyApp.Current!.Paths.Data, "MyApp")`.

## Notes

- `closeWhen` returning true **cancels** that navigation, so the callback URL never has to exist.
- Some providers refuse to run inside embedded web views (Google does). For those, open the system browser with [`UriLauncher`](../Niddy.Avalonia/Utilities/UriLauncher.md) and receive the redirect on a loopback listener or a custom URL scheme.
- On platforms without a web view (headless, Linux without WebKitGTK), the dialog shows a message and the address instead of failing. The user can only close it, so the callback check above returns false.
- Use PKCE (`code_challenge`) for public clients where the provider supports it.

## See also

- [settings-logging-paths](settings-logging-paths.md)
