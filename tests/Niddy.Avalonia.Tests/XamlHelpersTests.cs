using System.Globalization;
using Avalonia.Headless.XUnit;
using Niddy.Avalonia.Layout;
using Niddy.Avalonia.State;

namespace Niddy.Avalonia.Tests;

public class XamlHelpersTests
{
    [AvaloniaFact]
    public void TheNiddyXmlnsResolvesEveryHelper()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var previous = UiStateStore.Default;
        UiStateStore.Default = null;
        try
        {
            var window = new XamlHelpersWindow { DataContext = new XamlHelpersModel() };
            window.Show();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("Helpers", WindowMemory.GetKey(window));
            Assert.Equal("Helpers.Split", LayoutMemory.GetKey(window.Split));
            Assert.True(window.Dark.IsChecked);
            Assert.Equal([XamlHelpersMode.Light, XamlHelpersMode.Dark], window.Modes.Items.Cast<XamlHelpersMode>());
            Assert.Equal(XamlHelpersMode.Dark, window.Modes.SelectedItem);
            Assert.Equal("1.5 MB", window.Size.Text);
            Assert.True(window.Empty.IsVisible);
            Assert.Equal("narrow", Responsive.GetSizeClass(window.Panel));
            Assert.Equal(global::Avalonia.Layout.Orientation.Horizontal, window.Panel.Orientation);
        }
        finally
        {
            UiStateStore.Default = previous;
            CultureInfo.CurrentCulture = culture;
        }
    }
}
