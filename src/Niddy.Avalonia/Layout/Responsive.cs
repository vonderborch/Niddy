using Avalonia;
using Avalonia.Controls;

namespace Niddy.Avalonia.Layout;

/// <summary>
///     Switches a control between narrow and wide layouts by its own width: it gets the <c>narrow</c> style class
///     below <see cref="NarrowBelowProperty" /> and <c>wide</c> otherwise, so styles can rearrange it. Optionally a
///     <c>medium</c> class between <see cref="NarrowBelowProperty" /> and <see cref="WideAboveProperty" />.
/// </summary>
/// <example>
///     <code>
///         &lt;Grid niddy:Responsive.NarrowBelow="600" ColumnDefinitions="*,*"&gt;
///             &lt;Grid.Styles&gt;
///                 &lt;Style Selector="Grid.narrow"&gt;&lt;Setter Property="ColumnDefinitions" Value="*" /&gt;&lt;/Style&gt;
///             &lt;/Grid.Styles&gt;
///             ...
///         &lt;/Grid&gt;
///     </code>
/// </example>
public static class Responsive
{
    /// <summary>The class set below <see cref="NarrowBelowProperty" />.</summary>
    public const string NarrowClass = "narrow";

    /// <summary>The class set between the two widths when <see cref="WideAboveProperty" /> is set.</summary>
    public const string MediumClass = "medium";

    /// <summary>The class set at or above the wide width.</summary>
    public const string WideClass = "wide";

    /// <summary>
    ///     The width below which the control is narrow. Setting it starts tracking the control's width; NaN (the
    ///     default) stops it.
    /// </summary>
    public static readonly AttachedProperty<double> NarrowBelowProperty;

    /// <summary>
    ///     The width at or above which the control is wide. If not set (NaN, the default), it is
    ///     <see cref="NarrowBelowProperty" /> and there's no medium range.
    /// </summary>
    public static readonly AttachedProperty<double> WideAboveProperty;

    /// <summary>The control's current size class: <see cref="NarrowClass" />, <see cref="MediumClass" /> or <see cref="WideClass" />.</summary>
    public static readonly AttachedProperty<string?> SizeClassProperty;

    static Responsive()
    {
        NarrowBelowProperty = AvaloniaProperty.RegisterAttached<Control, double>("NarrowBelow", typeof(Responsive), double.NaN);
        WideAboveProperty = AvaloniaProperty.RegisterAttached<Control, double>("WideAbove", typeof(Responsive), double.NaN);
        SizeClassProperty = AvaloniaProperty.RegisterAttached<Control, string>("SizeClass", typeof(Responsive));
        NarrowBelowProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            Track(control);
        });
        WideAboveProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            Update(control, control.Bounds.Width);
        });
    }

    /// <summary>Gets the narrow width.</summary>
    public static double GetNarrowBelow(Control control)
    {
        return control.GetValue(NarrowBelowProperty);
    }

    /// <summary>Sets the narrow width.</summary>
    public static void SetNarrowBelow(Control control, double value)
    {
        control.SetValue(NarrowBelowProperty, value);
    }

    /// <summary>Gets the wide width.</summary>
    public static double GetWideAbove(Control control)
    {
        return control.GetValue(WideAboveProperty);
    }

    /// <summary>Sets the wide width.</summary>
    public static void SetWideAbove(Control control, double value)
    {
        control.SetValue(WideAboveProperty, value);
    }

    /// <summary>Gets the control's current size class, or null if it isn't tracked or hasn't been measured.</summary>
    public static string? GetSizeClass(Control control)
    {
        return control.GetValue(SizeClassProperty);
    }

    /// <summary>Works out the size class for a width.</summary>
    public static string Classify(double width, double narrowBelow, double wideAbove)
    {
        if (width < narrowBelow)
        {
            return "narrow";
        }
        return (double.IsNaN(wideAbove) || width >= wideAbove) ? "wide" : "medium";
    }

    private static void Track(Control control)
    {
        control.PropertyChanged -= OnPropertyChanged;
        if (double.IsNaN(GetNarrowBelow(control)))
        {
            control.Classes.Remove("narrow");
            control.Classes.Remove("medium");
            control.Classes.Remove("wide");
            control.ClearValue(SizeClassProperty);
        }
        else
        {
            control.PropertyChanged += OnPropertyChanged;
            if (control.Bounds.Width > 0.0)
            {
                Update(control, control.Bounds.Width);
            }
        }
    }

    private static void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty && sender is Control control)
        {
            Update(control, control.Bounds.Width);
        }
    }

    internal static void Update(Control control, double width)
    {
        double narrowBelow = GetNarrowBelow(control);
        if (!double.IsNaN(narrowBelow))
        {
            string text = Classify(width, narrowBelow, GetWideAbove(control));
            if (!(GetSizeClass(control) == text))
            {
                control.SetValue(SizeClassProperty, text);
                control.Classes.Set("narrow", text == "narrow");
                control.Classes.Set("medium", text == "medium");
                control.Classes.Set("wide", text == "wide");
            }
        }
    }
}
