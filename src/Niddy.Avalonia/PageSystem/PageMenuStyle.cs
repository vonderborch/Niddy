namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     How a <see cref="PageMenu" /> shows its items. The menu's <see cref="Placement" /> then decides which
///     edge it docks to and where along that edge the items (or the button) sit; a placement that doesn't fit the
///     style is read as the nearest one that does.
/// </summary>
public enum PageMenuStyle
{
    /// <summary>
    ///     A sidebar: items stacked in a column along the left or right edge, scrolling when they don't fit. The
    ///     placement's side picks the edge (the left for <c>TopCenter</c> and <c>BottomCenter</c>), and its top,
    ///     center or bottom puts the items at the top, middle or bottom of the column. Shows the tree in
    ///     <see cref="Tree" />.
    /// </summary>
    VerticalStrip,
    /// <summary>
    ///     A tab strip: items in a row along the top or bottom edge, scrolling sideways when they don't fit. The
    ///     placement's top or bottom picks the edge (the top for <c>CenterLeft</c> and <c>CenterRight</c>), and its
    ///     left, center or right puts the items at the start, middle or end of the row.
    /// </summary>
    HorizontalStrip,
    /// <summary>
    ///     A phone-style navigation bar: equal-width items, each an icon above its name, across the whole top or
    ///     bottom edge. The placement's top or bottom picks the edge (the bottom for <c>CenterLeft</c> and
    ///     <c>CenterRight</c>).
    /// </summary>
    NavigationBar,
    /// <summary>
    ///     A menu button that opens the items in a flyout. The button sits at the placement, in a strip along the top
    ///     or bottom edge (along the side for <c>CenterLeft</c> and <c>CenterRight</c>), and the flyout opens from
    ///     it toward the page. Shows the tree in <see cref="Tree" />.
    /// </summary>
    MenuButton
}
