using Avalonia.Layout;

namespace Niddy.Avalonia;

/// <summary>
///     One of the eight positions around the edge of the screen (or of the control hosting something): the four
///     corners and the middle of each side. Used by <see cref="PageMenu" /> and <see cref="ToastHost" />.
/// </summary>
public enum ScreenPlacement
{
    /// <summary>The top-left corner.</summary>
    TopLeft,
    /// <summary>The middle of the top edge.</summary>
    TopCenter,
    /// <summary>The top-right corner.</summary>
    TopRight,
    /// <summary>The middle of the left edge.</summary>
    CenterLeft,
    /// <summary>The middle of the right edge.</summary>
    CenterRight,
    /// <summary>The bottom-left corner.</summary>
    BottomLeft,
    /// <summary>The middle of the bottom edge.</summary>
    BottomCenter,
    /// <summary>The bottom-right corner.</summary>
    BottomRight
}

internal static class ScreenPlacementExtensions
{
    extension(ScreenPlacement placement)
    {
        public bool IsCorner
        {
            get
            {
                switch (placement)
                {
                case ScreenPlacement.TopLeft:
                case ScreenPlacement.TopRight:
                case ScreenPlacement.BottomLeft:
                case ScreenPlacement.BottomRight:
                    return true;
                default:
                    return false;
                }
            }
        }

        public bool IsTop
        {
            get
            {
                if ((uint)placement <= 2u)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsBottom
        {
            get
            {
                if ((uint)(placement - 5) <= 2u)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsRight
        {
            get
            {
                if (placement == ScreenPlacement.TopRight || placement == ScreenPlacement.CenterRight || placement == ScreenPlacement.BottomRight)
                {
                    return true;
                }
                return false;
            }
        }

        public HorizontalAlignment HorizontalAlignment
        {
            get
            {
                HorizontalAlignment result;
                switch (placement)
                {
                case ScreenPlacement.TopLeft:
                case ScreenPlacement.CenterLeft:
                case ScreenPlacement.BottomLeft:
                    result = HorizontalAlignment.Left;
                    break;
                case ScreenPlacement.TopRight:
                case ScreenPlacement.CenterRight:
                case ScreenPlacement.BottomRight:
                    result = HorizontalAlignment.Right;
                    break;
                default:
                    result = HorizontalAlignment.Center;
                    break;
                }
                return result;
            }
        }

        public VerticalAlignment VerticalAlignment
        {
            get
            {
                VerticalAlignment result;
                switch (placement)
                {
                case ScreenPlacement.TopLeft:
                case ScreenPlacement.TopCenter:
                case ScreenPlacement.TopRight:
                    result = VerticalAlignment.Top;
                    break;
                case ScreenPlacement.BottomLeft:
                case ScreenPlacement.BottomCenter:
                case ScreenPlacement.BottomRight:
                    result = VerticalAlignment.Bottom;
                    break;
                default:
                    result = VerticalAlignment.Center;
                    break;
                }
                return result;
            }
        }
    }
}
