using Avalonia.Metadata;

// One XAML namespace for the whole library: xmlns:niddy="https://github.com/vonderborch/Niddy".
// Every namespace with public types that can be used from XAML is listed here. Dialogs.BuiltIn holds only
// internal dialogs, so it isn't.
[assembly: XmlnsPrefix("https://github.com/vonderborch/Niddy", "niddy")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Converters")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.DataContexts")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Dialogs")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Hosting")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Layout")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.PageSystem")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.State")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Tables")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Theme")]
[assembly: XmlnsDefinition("https://github.com/vonderborch/Niddy", "Niddy.Avalonia.Toasts")]
