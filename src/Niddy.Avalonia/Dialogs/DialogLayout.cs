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
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogLayout, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<DialogLayout, string?>(nameof(Description));

    public static readonly StyledProperty<IBrush?> TitleForegroundProperty =
        AvaloniaProperty.Register<DialogLayout, IBrush?>(nameof(TitleForeground));

    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _description = new() { FontSize = 14, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly WrapPanel _buttons = new() { HorizontalAlignment = HorizontalAlignment.Right, ItemSpacing = 8, LineSpacing = 8 };

    public DialogLayout()
    {
        Template = new FuncControlTemplate<DialogLayout>((layout, _) =>
        {
            // Template parts are reused if the template is applied again, so detach them from the last one.
            foreach (var part in new Control[] { layout._title, layout._description, layout._buttons })
                (part.Parent as Panel)?.Children.Remove(part);

            var presenter = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = layout[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = layout[!ContentTemplateProperty],
            };
            return new StackPanel
            {
                Spacing = 12,
                Children = { layout._title, layout._description, presenter, layout._buttons },
            };
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
            SetText(_title, Title);
        else if (change.Property == DescriptionProperty)
            SetText(_description, Description);
        else if (change.Property == TitleForegroundProperty)
        {
            if (TitleForeground is { } brush)
                _title.Foreground = brush;
            else
                _title.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private static void SetText(TextBlock block, string? text)
    {
        block.Text = text;
        block.IsVisible = !string.IsNullOrEmpty(text);
    }
}
