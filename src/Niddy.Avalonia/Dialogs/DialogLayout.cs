using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Niddy.Avalonia.Dialogs;

/// <summary>
///     The standard dialog layout: a title, a description, the dialog's own content, then a row of
///     buttons aligned to the right that wraps on narrow screens. Every built-in dialog inherits it through
///     <see cref="DialogBase{TResult}"/>, and so does a custom dialog.
///     <para>
///         Use it as the root element of a dialog's AXAML file. Child content becomes the body, and the
///         title and description are hidden while empty:
///     </para>
///     <code>
///         &lt;dialogs:DialogLayout x:Class="MyApp.ColorDialog" Title="Pick a color"&gt;
///             &lt;ColorPicker Name="Picker" /&gt;
///             &lt;dialogs:DialogLayout.Buttons&gt;
///                 &lt;Button Content="OK" IsDefault="True" Click="Ok_OnClick" /&gt;
///             &lt;/dialogs:DialogLayout.Buttons&gt;
///         &lt;/dialogs:DialogLayout&gt;
///     </code>
/// </summary>
public class DialogLayout : UserControl
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogLayout, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<DialogLayout, string?>(nameof(Description));

    /// <summary>Defines the <see cref="TitleForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TitleForegroundProperty =
        AvaloniaProperty.Register<DialogLayout, IBrush?>(nameof(TitleForeground));

    // The gaps are margins rather than panel spacing, so a hidden part takes no space.
    private const double Gap = 12;

    private readonly TextBlock _title = new()
    {
        FontSize = 20, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 0, 0, Gap),
    };

    private readonly TextBlock _description = new()
    {
        FontSize = 14, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 0, 0, Gap),
    };

    private readonly WrapPanel _buttons = new()
    {
        HorizontalAlignment = HorizontalAlignment.Right, ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(0, Gap, 0, 0),
    };

    private RowDefinition? _contentRow;
    private bool _fillHeight;

    /// <summary>Creates an empty layout.</summary>
    public DialogLayout()
    {
        Template = new FuncControlTemplate<DialogLayout>((layout, _) =>
        {
            // Template parts are reused if the template is applied again, so detach them from the last one.
            foreach (Control part in new Control[] { layout._title, layout._description, layout._buttons })
            {
                (part.Parent as Panel)?.Children.Remove(part);
            }

            ContentPresenter presenter = new()
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = layout[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = layout[!ContentTemplateProperty],
            };
            layout._contentRow = new RowDefinition(layout.ContentRowHeight);
            Grid grid = new()
            {
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), layout._contentRow, new RowDefinition(GridLength.Auto) },
                Children = { layout._title, layout._description, presenter, layout._buttons },
            };
            Grid.SetRow(layout._description, 1);
            Grid.SetRow(presenter, 2);
            Grid.SetRow(layout._buttons, 3);
            return grid;
        });
        _buttons.Children.CollectionChanged += (_, _) => _buttons.IsVisible = _buttons.Children.Count > 0;
        _buttons.IsVisible = false;
    }

    /// <summary>The title, shown in bold above the description. Also the window title in window mode.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The description, shown below the title.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>The title's brush. Null (the default) uses the normal text color.</summary>
    public IBrush? TitleForeground
    {
        get => GetValue(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }

    /// <summary>The buttons shown along the bottom, right-aligned. The row is hidden while empty.</summary>
    public Controls Buttons => _buttons.Children;

    /// <summary>
    ///     Whether the content takes all the height left over by the title, description and buttons, when the host gives
    ///     the dialog a fixed height. False (the default) sizes the content to fit. Hosts fill the space they have for a
    ///     dialog with this set, rather than scrolling it.
    /// </summary>
    internal bool FillHeight
    {
        get => _fillHeight;
        set
        {
            _fillHeight = value;
            if (_contentRow is not null)
            {
                _contentRow.Height = ContentRowHeight;
            }
        }
    }

    private GridLength ContentRowHeight => _fillHeight ? GridLength.Star : GridLength.Auto;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            SetText(_title, Title);
        }
        else if (change.Property == DescriptionProperty)
        {
            SetText(_description, Description);
        }
        else if (change.Property == TitleForegroundProperty)
        {
            if (TitleForeground is { } brush)
            {
                _title.Foreground = brush;
            }
            else
            {
                _title.ClearValue(TextBlock.ForegroundProperty);
            }
        }
    }

    /// <summary>
    ///     Gives a built-in dialog's scrolling area the height a caller asked for, in place of its default maximum.
    ///     Null leaves the default: the area fits its content up to that maximum, then scrolls.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> isn't a positive, finite number.</exception>
    internal static void SetContentHeight(Control area, double? height)
    {
        if (height is not { } value)
        {
            return;
        }

        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException("contentHeight", value, "The content height must be a positive, finite number.");
        }

        area.MaxHeight = double.PositiveInfinity;
        area.Height = value;
    }

    private static void SetText(TextBlock block, string? text)
    {
        block.Text = text;
        block.IsVisible = !string.IsNullOrEmpty(text);
    }
}
