namespace Niddy.Avalonia.Toasts;

/// <summary>A toast's style: its color and icon.</summary>
public enum ToastType
{
    /// <summary>Neutral information.</summary>
    Info,
    /// <summary>Something finished successfully.</summary>
    Success,
    /// <summary>Something needs attention but didn't fail.</summary>
    Warning,
    /// <summary>Something failed.</summary>
    Error,
}
