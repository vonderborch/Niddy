namespace Niddy.Avalonia.Dialogs;

/// <summary>
///     How <see cref="Dialog.View.Open"/> fits a view into its area: the space between the description and the buttons.
///     The area is as wide as the dialog. Its height is <c>contentHeight</c> when given, or everything left over with
///     <c>fill</c>; otherwise it depends on the fit.
/// </summary>
public enum ViewFit
{
    /// <summary>
    ///     The view is laid out like any control: it's given the area's width, and its own <c>HorizontalAlignment</c> and
    ///     <c>VerticalAlignment</c> decide whether it stretches across it. Without a fixed height, the area is as tall as the
    ///     view wants and the whole dialog scrolls if it's too tall. The default.
    /// </summary>
    Stretch,

    /// <summary>
    ///     The view keeps the size it wants, centered in the area. A view bigger than the area is limited to it, as with
    ///     <see cref="Stretch"/>.
    /// </summary>
    Center,

    /// <summary>
    ///     The view keeps the size it wants, and scrolls inside the area, both ways, when it's bigger. The title,
    ///     description and buttons stay in place. Without a fixed height, the area fits the view up to 400, then scrolls.
    /// </summary>
    Scroll,

    /// <summary>
    ///     The view keeps the size it wants, but is scaled down, keeping its proportions, when it's bigger than the area.
    ///     Without a fixed height, the area is at most 400 tall.
    /// </summary>
    Shrink,

    /// <summary>
    ///     The view is scaled up or down, keeping its proportions, to fit the area. Without a fixed height, the area is at
    ///     most 400 tall.
    /// </summary>
    Scale,
}
