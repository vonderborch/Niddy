using Avalonia.Interactivity;
using Avalonia.Media;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A color picker dialog. The result is the chosen color, or null if cancelled.</summary>
internal sealed partial class ColorDialog : DialogBase<Color?>
{
    public ColorDialog()
    {
        InitializeComponent();
    }

    internal ColorDialog(
        string title,
        string description,
        Color initialColor,
        bool showAlpha,
        IEnumerable<Color>? palette,
        string okButtonText,
        string cancelButtonText
    ) : this()
    {
        Title = title;
        Description = description;
        ColorViewPicker.Color = initialColor;
        ColorViewPicker.IsAlphaVisible = showAlpha;
        if (palette is not null)
            ColorViewPicker.PaletteColors = palette.ToList();
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
    }

    /// <summary>The color currently chosen in the picker.</summary>
    internal Color SelectedColor
    {
        get => ColorViewPicker.Color;
        set => ColorViewPicker.Color = value;
    }

    protected override Color? DismissResult => null;

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e) => Close(ColorViewPicker.Color);
}
