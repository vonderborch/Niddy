namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Gives a <see cref="PageRegistrationAttribute"/> page its <see cref="PageIcon"/>. Repeat it to offer several
///     sizes, e.g. a simpler path at 16 and a detailed one at 24, or bitmaps at 24 and 48 for high-DPI screens; the
///     best one is picked for the size it is shown at (see <see cref="PageIcon.GetSource"/>). Set exactly one of
///     <see cref="Data"/>, <see cref="Source"/> or <see cref="ResourceKey"/>.
///     <para>Read at compile time by the <c>Niddy.Avalonia.Generators</c> package, like the registration.</para>
/// </summary>
/// <example>
///     <code>
///         [PageRegistration]
///         [PageIcon("M4 10 L12 3 L20 10 V20 H4 Z", Size = 24)]
///         [PageIcon(Source = "avares://MyApp/Assets/home-48.png", Size = 48)]
///         public partial class HomePage : Page;
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PageIconAttribute : Attribute
{
    /// <summary>Creates an icon source; set <see cref="Data"/>, <see cref="Source"/> or <see cref="ResourceKey"/>.</summary>
    public PageIconAttribute()
    {
    }

    /// <summary>Creates a vector icon source from SVG-style path data.</summary>
    /// <param name="data">The path data, e.g. <c>"M3 12 L12 3 L21 12 Z"</c>.</param>
    public PageIconAttribute(string data) => Data = data;

    /// <summary>SVG-style path data, filled with the foreground color. See <see cref="PageIconSource.FromData"/>.</summary>
    public string? Data { get; set; }

    /// <summary>
    ///     A bitmap's absolute URI, e.g. <c>avares://MyApp/Assets/home-24.png</c>. See <see cref="PageIconSource.FromUri"/>.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///     The key of a <c>Geometry</c> or image resource, e.g. a <c>StreamGeometry</c> in the app's resources, looked up
    ///     where the icon is shown so it can follow the theme. See <see cref="PageIconSource.FromResource"/>.
    /// </summary>
    public string? ResourceKey { get; set; }

    /// <summary>
    ///     The size this source is made for, or 0 (the default) for any size: the side of the square path data is drawn
    ///     in (like an SVG <c>viewBox</c>), or a bitmap's width in pixels.
    /// </summary>
    public double Size { get; set; }
}
