namespace Niddy.Avalonia.PageSystem;

/// <summary>Which pages a <see cref="PageMenu" /> lists.</summary>
public enum PageMenuMode
{
    /// <summary>One level: the child pages of <see cref="ParentPage" />, or the top-level pages.</summary>
    Flat,
    /// <summary>
    ///     The whole tree below <see cref="ParentPage" />, as expandable, indented items. Applies to the
    ///     <see cref="VerticalStrip" /> and <see cref="MenuButton" /> styles; a horizontal
    ///     strip or navigation bar stays <see cref="Flat" />.
    /// </summary>
    Tree
}
