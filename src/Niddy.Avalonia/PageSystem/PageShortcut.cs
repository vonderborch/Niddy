using Avalonia.Input;

namespace Niddy.Avalonia.PageSystem;

/// <summary>Parses page shortcuts, which are key gestures with a platform-neutral <c>Primary</c> modifier.</summary>
public static class PageShortcut
{
    /// <summary>The modifier <c>Primary</c> stands for: Meta (Cmd) on macOS and iOS, Control elsewhere.</summary>
    public static KeyModifiers PrimaryModifier { get; } =
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>
    ///     Parses a key gesture such as <c>"Primary+1"</c>, <c>"Ctrl+Shift+S"</c> or <c>"F5"</c>. <c>Primary</c> is
    ///     replaced with <see cref="PrimaryModifier"/>; everything else follows <see cref="KeyGesture.Parse"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The text isn't a valid key gesture.</exception>
    public static KeyGesture Parse(string text) => Parse(text, nameof(text));

    /// <summary>Tries to parse a key gesture; see <see cref="Parse(string)"/>.</summary>
    public static bool TryParse(string? text, out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        try
        {
            gesture = Parse(text);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Formats a gesture for display, e.g. <c>⌘1</c> on macOS or <c>Ctrl+1</c> elsewhere.</summary>
    public static string Format(KeyGesture gesture) =>
        gesture.ToString(null, System.Globalization.CultureInfo.CurrentCulture);

    internal static KeyGesture Parse(string text, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, paramName);

        var primary = PrimaryModifier == KeyModifiers.Meta ? "Meta" : "Ctrl";
        var parts = text.Split('+');

        // "Ctrl++" is Ctrl and the plus key, so only rewrite modifier positions.
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Trim().Equals("Primary", StringComparison.OrdinalIgnoreCase))
                parts[i] = primary;
        }

        // KeyGesture would read "1" as the key with value 1 rather than the 1 key.
        var last = parts[^1].Trim();
        if (last.Length == 1 && char.IsAsciiDigit(last[0]))
            parts[^1] = "D" + last;

        try
        {
            var gesture = KeyGesture.Parse(string.Join('+', parts));
            if (gesture.Key == Key.None)
                throw new ArgumentException($"'{text}' has no key.", paramName);
            return gesture;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or InvalidOperationException)
        {
            throw new ArgumentException($"'{text}' isn't a valid key gesture, e.g. \"Primary+1\" or \"Ctrl+Shift+S\".", paramName, e);
        }
    }
}
