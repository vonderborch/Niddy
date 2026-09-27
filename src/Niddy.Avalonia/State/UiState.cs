using System.Collections.Concurrent;
using Avalonia.Controls;

namespace Niddy.Avalonia.State;

/// <summary>What <see cref="UiStateStore"/> saves: window placements and layout sizes, by key.</summary>
public sealed class UiState
{
    /// <summary>Window placements, keyed by <see cref="WindowMemory"/> key.</summary>
    public ConcurrentDictionary<string, WindowPlacement> Windows { get; set; } = new();

    /// <summary>Layout sizes, keyed by <see cref="LayoutMemory"/> key.</summary>
    public ConcurrentDictionary<string, string[]> Layouts { get; set; } = new();
}

/// <summary>Where a window was and how big, when it wasn't maximized, and whether it was maximized or full screen.</summary>
/// <param name="Width">The window's width when in the normal state, in device-independent pixels.</param>
/// <param name="Height">The window's height when in the normal state, in device-independent pixels.</param>
/// <param name="X">The window's left edge when in the normal state, in screen pixels; null if unknown.</param>
/// <param name="Y">The window's top edge when in the normal state, in screen pixels; null if unknown.</param>
/// <param name="State">Normal, maximized or full screen. Minimized windows are restored as normal.</param>
public sealed record WindowPlacement(double Width, double Height, int? X = null, int? Y = null, WindowState State = WindowState.Normal);
