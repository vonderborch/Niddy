using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     A page's icon, e.g. for a <see cref="PageMenu" />, in one or more sizes. Each size is a
///     <see cref="PageIconSource" />: vector path data, a bitmap, or a resource. <see cref="GetSource(double,double)" /> picks the best
///     one for the size it is shown at, and <see cref="PageIconView" /> shows it.
///     <para>
///         Give a page its icon with <see cref="PageIconAttribute" />, once per size, or pass one to
///         <see cref="PageRegistry" />'s <c>Register</c>.
///     </para>
/// </summary>
public sealed class PageIcon : IEquatable<PageIcon>
{
    /// <summary>The icon's sources, in the order given.</summary>
    public IReadOnlyList<PageIconSource> Sources { get; }

    /// <summary>Creates an icon from its sources, e.g. one per size.</summary>
    /// <exception cref="ArgumentException">No sources were given.</exception>
    public PageIcon(params IEnumerable<PageIconSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Sources = [.. sources];
        if (Sources.Count == 0)
        {
            throw new ArgumentException("An icon needs at least one source.", "sources");
        }
        if (Sources.Any((PageIconSource s) => s == null))
        {
            throw new ArgumentException("Icon sources can't be null.", "sources");
        }
    }

    /// <summary>Creates a vector icon from SVG-style path data, e.g. <c>"M3 12 L12 3 L21 12 Z"</c>.</summary>
    /// <inheritdoc cref="FromData(string,double)" />
    public static PageIcon FromData(string data, double size = 0.0)
    {
        return new PageIcon(PageIconSource.FromData(data, size));
    }

    /// <summary>
    ///     Picks the source to show at <paramref name="size" />, in this order:
    ///     <list type="number">
    ///         <item>one made for exactly that size (for a bitmap, that many pixels at <paramref name="scaling" />);</item>
    ///         <item>a scalable one (path data, geometry or resource) for any size;</item>
    ///         <item>the scalable one made for the nearest size;</item>
    ///         <item>the smallest bitmap at least as big, so it is scaled down rather than up;</item>
    ///         <item>a bitmap for any size;</item>
    ///         <item>the largest bitmap.</item>
    ///     </list>
    /// </summary>
    /// <param name="size">The size it is shown at, in device-independent pixels.</param>
    /// <param name="scaling">The screen's scaling, e.g. 2 on a high-DPI screen, to pick bitmaps by physical pixels.</param>
    public PageIconSource GetSource(double size, double scaling = 1.0)
    {
        double pixels = size * scaling;
        List<PageIconSource> source = Sources.Where((PageIconSource s) => s.IsScalable).ToList();
        List<PageIconSource> source2 = Sources.Where((PageIconSource s) => !s.IsScalable).ToList();
        return source.FirstOrDefault((PageIconSource s) => s.Size > 0.0 && s.Size == size) ?? source2.FirstOrDefault((PageIconSource s) => s.Size > 0.0 && s.Size == pixels) ?? source.FirstOrDefault((PageIconSource s) => s.Size == 0.0) ?? (from s in source
            orderby Math.Abs(s.Size - size), s.Size descending
            select s).FirstOrDefault() ?? source2.Where((PageIconSource s) => s.Size >= pixels).MinBy((PageIconSource s) => s.Size) ?? source2.FirstOrDefault((PageIconSource s) => s.Size == 0.0) ?? source2.MaxBy((PageIconSource s) => s.Size);
    }

    /// <inheritdoc />
    public bool Equals(PageIcon? other)
    {
        return other != null && Sources.SequenceEqual(other.Sources);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as PageIcon);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Sources.Aggregate(17, (int hash, PageIconSource source) => hash * 31 + source.GetHashCode());
    }
}

/// <summary>One size of a <see cref="PageIcon" />: vector path data, a bitmap, or a resource.</summary>
public sealed class PageIconSource : IEquatable<PageIconSource>
{
    private enum SourceKind
    {
        Data,
        Uri,
        Resource,
        Geometry,
        Image
    }

    private readonly SourceKind _kind;

    private readonly object _value;

    private object? _loaded;

    private bool _isLoaded;

    /// <summary>
    ///     The size the source is made for, or 0 for any size. For vectors, it is the side of the square the path is
    ///     drawn in, like an SVG <c>viewBox</c>: path data for a 24×24 icon set has a size of 24. For bitmaps, it is
    ///     the width in pixels.
    /// </summary>
    public double Size { get; }

    /// <summary>Whether the source scales cleanly to any size: path data, a geometry, or a resource.</summary>
    public bool IsScalable
    {
        get
        {
            SourceKind kind = _kind;
            bool flag = ((kind == SourceKind.Uri || kind == SourceKind.Image) ? true : false);
            return !flag;
        }
    }

    /// <summary>The SVG-style path data, if the source is path data.</summary>
    public string? Data => (_kind == SourceKind.Data) ? ((string)_value) : null;

    /// <summary>The bitmap's URI, if the source is a bitmap URI.</summary>
    public string? Uri => (_kind == SourceKind.Uri) ? ((string)_value) : null;

    /// <summary>The resource key, if the source is a resource.</summary>
    public object? ResourceKey => (_kind == SourceKind.Resource) ? _value : null;

    private PageIconSource(SourceKind kind, object value, double size)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (value is string argument)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument);
        }
        _kind = kind;
        _value = value;
        Size = size;
    }

    /// <summary>
    ///     A vector source from SVG-style path data, e.g. <c>"M3 12 L12 3 L21 12 Z"</c>, filled with the foreground
    ///     color. Without a size, the path is scaled to fit; with one, it is drawn in a square of that size.
    /// </summary>
    /// <param name="data">The path data.</param>
    /// <param name="size">The side of the square the path is drawn in, e.g. 24, or 0 to fit the path's bounds.</param>
    public static PageIconSource FromData(string data, double size = 0.0)
    {
        return new PageIconSource(SourceKind.Data, data, size);
    }

    /// <summary>
    ///     A bitmap source, loaded the first time it is shown: an <c>avares://</c> asset (e.g.
    ///     <c>avares://MyApp/Assets/home-24.png</c>) or a <c>file://</c> path.
    /// </summary>
    /// <param name="uri">The bitmap's absolute URI.</param>
    /// <param name="size">The bitmap's width in pixels, or 0 to use it at any size.</param>
    public static PageIconSource FromUri(string uri, double size = 0.0)
    {
        return new PageIconSource(SourceKind.Uri, uri, size);
    }

    /// <summary>
    ///     A <see cref="Geometry" /> or <see cref="IImage" /> resource, looked up from where the icon is shown, so it
    ///     can differ per theme, e.g. a <c>StreamGeometry</c> in the app's resources.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="size">For a geometry, the side of the square it is drawn in, or 0 to fit its bounds.</param>
    public static PageIconSource FromResource(object key, double size = 0.0)
    {
        return new PageIconSource(SourceKind.Resource, key, size);
    }

    /// <summary>A vector source from a geometry, filled with the foreground color.</summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="size">The side of the square it is drawn in, or 0 to fit its bounds.</param>
    public static PageIconSource FromGeometry(Geometry geometry, double size = 0.0)
    {
        return new PageIconSource(SourceKind.Geometry, geometry, size);
    }

    /// <summary>A source from an image, e.g. a <see cref="Bitmap" />.</summary>
    /// <param name="image">The image.</param>
    /// <param name="size">The image's width in pixels, or 0 to use it at any size.</param>
    public static PageIconSource FromImage(IImage image, double size = 0.0)
    {
        return new PageIconSource(SourceKind.Image, image, size);
    }

    /// <summary>The geometry or image to draw, or null if it can't be loaded or the resource isn't found.</summary>
    internal object? Resolve(Control control)
    {
        if (_kind == SourceKind.Resource)
        {
            object value;
            bool flag = control.TryFindResource(_value, control.ActualThemeVariant, out value);
            bool flag2 = flag;
            if (flag2)
            {
                bool flag3 = ((value is Geometry || value is IImage) ? true : false);
                flag2 = flag3;
            }
            return flag2 ? value : null;
        }
        if (!_isLoaded)
        {
            _loaded = Load();
            _isLoaded = true;
        }
        return _loaded;
    }

    private object? Load()
    {
        try
        {
            SourceKind kind = _kind;
            object result;
            switch (kind)
            {
            case SourceKind.Data:
                result = Geometry.Parse((string)_value);
                break;
            case SourceKind.Uri:
            {
                Uri uri = new Uri((string)_value);
                result = (((object)uri == null || !uri.IsFile) ? new Bitmap(AssetLoader.Open(new Uri((string)_value))) : new Bitmap(uri.LocalPath));
                break;
            }
            default:
                result = _value;
                break;
            }
            return result;
        }
        catch (Exception ex) when (((ex is FormatException || ex is IOException || ex is ArgumentException || ex is InvalidOperationException) ? 1 : 0) != 0)
        {
            Trace.TraceWarning($"Page icon '{_value}' can't be loaded: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public bool Equals(PageIconSource? other)
    {
        return other != null && _kind == other._kind && Size == other.Size && object.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as PageIconSource);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(_kind, _value, Size);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return (Size > 0.0) ? $"{_value} ({Size})" : (_value.ToString() ?? _kind.ToString());
    }
}
