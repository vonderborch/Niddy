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
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        UiStateStore? previous = UiStateStore.Default;
        UiStateStore.Default = null;
        try
        {
            XamlHelpersWindow window = new() { DataContext = new XamlHelpersModel() };
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
