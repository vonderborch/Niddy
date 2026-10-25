using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Niddy.Avalonia.Dialogs.BuiltIn;

/// <summary>
/// A dialog showing any control, or a window's content, under the standard title and description. The result is true
/// for OK, false if cancelled.
/// </summary>
internal sealed partial class ViewDialog : DialogBase<bool>
{
    /// <summary>The area's maximum height for the fits that scroll or scale, when it has no fixed height.</summary>
    private const double FitMaxHeight = 400;

    private readonly Func<bool>? _canAccept;

    public ViewDialog()
    {
        InitializeComponent();
    }

    internal ViewDialog(
        string? title,
        string? description,
        string? okButtonText,
        string? cancelButtonText,
        Func<bool>? canAccept,
        double? contentHeight,
        ViewFit fit,
        bool fill
    ) : this()
    {
        Title = title;
        Description = description;
        FillHeight = fill;
        Fit(fit, fill);
        SetContentHeight(Area, contentHeight);
        _canAccept = canAccept;
        ButtonOk.Content = okButtonText;
        ButtonOk.IsVisible = okButtonText is not null;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = cancelButtonText is not null;
    }

    protected override bool CanDismiss => ButtonCancel.IsVisible;

    protected override bool DismissResult => false;

    /// <summary>Finds the view dialog a control is shown in, or null.</summary>
    internal static ViewDialog? Find(Control control) => control.FindLogicalAncestorOfType<ViewDialog>(includeSelf: true);

    /// <summary>Closes the dialog with a result, as <see cref="Dialog.View.Close"/> does.</summary>
    internal void CloseWith(bool accepted) => Close(accepted);

    /// <summary>Takes the content out of the dialog, so it can be shown again or go back to its window.</summary>
    internal void Release()
    {
        Body.Content = null;
        Body.ContentTemplate = null;
        Body.ClearValue(DataContextProperty);
    }

    private void Fit(ViewFit fit, bool fill)
    {
        if (fit is ViewFit.Center)
        {
            Body.HorizontalContentAlignment = HorizontalAlignment.Center;
            Body.VerticalContentAlignment = VerticalAlignment.Center;
            return;
        }

        Control? wrapper = fit switch
        {
            ViewFit.Stretch => null,
            ViewFit.Scroll => new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            ViewFit.Shrink => new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly },
            ViewFit.Scale => new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both },
            _ => throw new ArgumentOutOfRangeException(nameof(fit), fit, "Not a ViewFit value."),
        };
        if (wrapper is null)
        {
            return;
        }

        Area.Child = null;
        if (wrapper is ScrollViewer scrollViewer)
        {
            scrollViewer.Content = Body;
        }
        else
        {
            ((Viewbox)wrapper).Child = Body;
        }

        Area.Child = wrapper;
        if (!fill)
        {
            Area.MaxHeight = FitMaxHeight;
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_canAccept?.Invoke() != false)
        {
            Close(true);
        }
    }
}
