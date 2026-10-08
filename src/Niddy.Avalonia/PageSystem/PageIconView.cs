using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Shows a <see cref="PageIcon" /> at <see cref="IconSize" />, picking the best of its sources for that size and
///     the screen's scaling. Vector icons are filled with <see cref="Foreground" />, inherited like text color, so they
///     follow the theme and a selected item's highlight. Takes no space without an icon.
///     <code>
///         &lt;pageSystem:PageIconView Icon="{Binding Icon}" IconSize="24" /&gt;
///     </code>
/// </summary>
public class PageIconView : Control
{
    /// <summary>Defines the <see cref="Icon" /> property.</summary>
    public static readonly StyledProperty<PageIcon?> IconProperty;

    /// <summary>Defines the <see cref="IconSize" /> property.</summary>
    public static readonly StyledProperty<double> IconSizeProperty;

    /// <summary>Defines the <see cref="Foreground" /> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty;

    /// <summary>The icon to show.</summary>
    public PageIcon? Icon
    {
        get
        {
            return GetValue(IconProperty);
        }
        set
        {
            SetValue(IconProperty, value);
        }
    }

    /// <summary>The icon's width and height, in device-independent pixels. Defaults to 20.</summary>
    public double IconSize
    {
        get
        {
            return GetValue(IconSizeProperty);
        }
        set
        {
            SetValue(IconSizeProperty, value);
        }
    }

    /// <summary>The color vector icons are filled with. Inherited, like text color.</summary>
    public IBrush? Foreground
    {
        get
        {
            return GetValue(ForegroundProperty);
        }
        set
        {
            SetValue(ForegroundProperty, value);
        }
    }

    /// <summary>The source picked for the current size and scaling, or null without an icon.</summary>
    public PageIconSource? CurrentSource => Icon?.GetSource(IconSize, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);

    static PageIconView()
    {
        IconProperty = AvaloniaProperty.Register<PageIconView, PageIcon?>(nameof(Icon));
        IconSizeProperty = AvaloniaProperty.Register<PageIconView, double>(nameof(IconSize), 20.0);
        ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PageIconView>();
        AffectsRender<PageIconView>(IconProperty, IconSizeProperty, ForegroundProperty);
        AffectsMeasure<PageIconView>(IconProperty, IconSizeProperty);
    }

    /// <summary>Creates an empty icon view.</summary>
    public PageIconView()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        return Icon == null ? default : new Size(IconSize, IconSize);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        PageIconSource? currentSource = CurrentSource;
        if (currentSource == null || IconSize <= 0.0)
        {
            return;
        }

        double iconSize = IconSize;
        Rect rect = new((Bounds.Width - iconSize) / 2.0, (Bounds.Height - iconSize) / 2.0, iconSize, iconSize);
        object? resolved = currentSource.Resolve(this);
        if (resolved is not Geometry geometry)
        {
            if (resolved is IImage { Size: { Width: > 0.0, Height: > 0.0 } } image)
            {
                context.DrawImage(image, new Rect(image.Size), rect);
            }

            return;
        }
        if (Transform(geometry, currentSource.Size, rect) is not { } matrix)
        {
            return;
        }
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(Foreground, null, geometry);
        }
    }

    /// <summary>Maps the geometry into <paramref name="box" />: its square of <paramref name="designSize" />, or its bounds.</summary>
    private static Matrix? Transform(Geometry geometry, double designSize, Rect box)
    {
        if (designSize > 0.0)
        {
            return Matrix.CreateScale(box.Width / designSize, box.Height / designSize) * Matrix.CreateTranslation(box.X, box.Y);
        }
        Rect bounds = geometry.Bounds;
        if (bounds.Width <= 0.0 && bounds.Height <= 0.0)
        {
            return null;
        }
        double scale = Math.Min(
            bounds.Width > 0.0 ? box.Width / bounds.Width : double.PositiveInfinity,
            bounds.Height > 0.0 ? box.Height / bounds.Height : double.PositiveInfinity
        );
        return Matrix.CreateTranslation(-bounds.X, -bounds.Y)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(box.X + (box.Width - bounds.Width * scale) / 2.0, box.Y + (box.Height - bounds.Height * scale) / 2.0);
    }
}
