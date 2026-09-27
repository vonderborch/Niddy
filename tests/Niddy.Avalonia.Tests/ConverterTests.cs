using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Niddy.Avalonia.Converters;
using Niddy.Avalonia.Layout;

namespace Niddy.Avalonia.Tests;

public class ConverterTests
{
    private enum Mode { Light, Dark }

    private static object? Convert(global::Avalonia.Data.Converters.IValueConverter converter, object? value, object? parameter = null) =>
        converter.Convert(value, typeof(object), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void EnumToBool_MatchesByValueOrName()
    {
        var converter = EnumToBoolConverter.Instance;
        Assert.Equal(true, Convert(converter, Mode.Dark, "Dark"));
        Assert.Equal(true, Convert(converter, Mode.Dark, Mode.Dark));
        Assert.Equal(false, Convert(converter, Mode.Light, "dark"));
        Assert.Equal(false, Convert(converter, null, "Dark"));
    }

    [Fact]
    public void EnumToBool_ConvertsBackOnlyWhenChecked()
    {
        var converter = EnumToBoolConverter.Instance;
        Assert.Equal(Mode.Dark, converter.ConvertBack(true, typeof(Mode), "Dark", CultureInfo.InvariantCulture));
        Assert.Equal(Mode.Light, converter.ConvertBack(true, typeof(Mode?), Mode.Light, CultureInfo.InvariantCulture));
        Assert.Same(BindingOperations.DoNothing, converter.ConvertBack(false, typeof(Mode), "Dark", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BoolToValue_PicksTheValue()
    {
        var converter = new BoolToValueConverter { TrueValue = "Online", FalseValue = "Offline" };
        Assert.Equal("Online", Convert(converter, true));
        Assert.Equal("Offline", Convert(converter, false));
        Assert.Equal("Offline", Convert(converter, null));
        Assert.Equal(true, converter.ConvertBack("Online", typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.ConvertBack("Offline", typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Humanize_FormatsValues()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("1.5 MB", Convert(HumanizeConverters.FileSize, 1_500_000L));
            Assert.Equal("1.5 MB", Convert(HumanizeConverters.FileSize, 1_500_000));
            Assert.Equal("12.3K", Convert(HumanizeConverters.Count, 12_345));
            Assert.Equal("5 min 3 s", Convert(HumanizeConverters.Duration, TimeSpan.FromSeconds(303)));
            Assert.Equal("just now", Convert(HumanizeConverters.RelativeTime, DateTimeOffset.Now));
            Assert.Null(Convert(HumanizeConverters.FileSize, "big"));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Collections_DetectEmptiness()
    {
        Assert.Equal(true, Convert(CollectionConverters.IsEmpty, null));
        Assert.Equal(true, Convert(CollectionConverters.IsEmpty, new List<int>()));
        Assert.Equal(true, Convert(CollectionConverters.IsEmpty, 0));
        Assert.Equal(true, Convert(CollectionConverters.IsEmpty, Enumerable.Empty<int>().Select(i => i)));
        Assert.Equal(false, Convert(CollectionConverters.IsEmpty, new ObservableCollection<int> { 1 }));
        Assert.Equal(true, Convert(CollectionConverters.IsNotEmpty, new[] { 1 }));
    }

    [Fact]
    public void EnumValues_ListsTheValues()
    {
        var values = new EnumValuesExtension(typeof(Mode?)).ProvideValue(null!);
        Assert.Equal([Mode.Light, Mode.Dark], ((Array)values).Cast<Mode>());
        Assert.Throws<ArgumentException>(() => new EnumValuesExtension(typeof(string)).ProvideValue(null!));
    }

    [Theory]
    [InlineData(300, double.NaN, "narrow")]
    [InlineData(700, double.NaN, "wide")]
    [InlineData(700, 1000, "medium")]
    [InlineData(1200, 1000, "wide")]
    public void Responsive_Classifies(double width, double wideAbove, string expected) =>
        Assert.Equal(expected, Responsive.Classify(width, 600, wideAbove));

    [AvaloniaFact]
    public void Responsive_SetsClassesFromTheWidth()
    {
        var panel = new StackPanel();
        Responsive.SetNarrowBelow(panel, 600);
        var window = new Window { Width = 400, Height = 300, Content = panel };
        window.Show();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("narrow", panel.Classes);
        Assert.Equal("narrow", Responsive.GetSizeClass(panel));

        window.Width = 900;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Contains("wide", panel.Classes);
        Assert.DoesNotContain("narrow", panel.Classes);

        Responsive.SetNarrowBelow(panel, double.NaN);
        Assert.DoesNotContain("wide", panel.Classes);
        Assert.Null(Responsive.GetSizeClass(panel));
    }
}
