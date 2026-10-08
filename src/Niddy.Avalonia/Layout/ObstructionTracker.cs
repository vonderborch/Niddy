using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace Niddy.Avalonia.Layout;

/// <summary>
///     Tracks how much of a control is covered by platform obstructions: safe-area insets (notch,
///     status bar, navigation bar, home indicator) and the on-screen keyboard. Reports the result as
///     a <see cref="Thickness"/> so overlay content can be kept clear of them.
///     <para>
///         Only the part of each obstruction that actually overlaps the control is reported. Avalonia
///         already pads a <see cref="TopLevel"/>'s root content by the safe area by default, in which
///         case this reports zero for those edges instead of padding twice.
///     </para>
/// </summary>
internal sealed class ObstructionTracker
{
    private readonly Control _control;
    private readonly Action<Thickness> _onChanged;
    private TopLevel? _topLevel;
    private Rect _keyboard;
    private Thickness _last;

    internal ObstructionTracker(Control control, Action<Thickness> onChanged)
    {
        _control = control;
        _onChanged = onChanged;
        _control.AttachedToVisualTree += (_, _) => Attach();
        _control.DetachedFromVisualTree += (_, _) => Detach();
        _control.SizeChanged += (_, _) => Update();
    }

    private void Attach()
    {
        Detach();
        _topLevel = TopLevel.GetTopLevel(_control);
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.SizeChanged += OnTopLevelSizeChanged;
        if (_topLevel.InsetsManager is { } insets)
        {
            insets.SafeAreaChanged += OnSafeAreaChanged;
        }

        if (_topLevel.InputPane is { } inputPane)
        {
            inputPane.StateChanged += OnInputPaneStateChanged;
            _keyboard = inputPane.State == InputPaneState.Open ? inputPane.OccludedRect : default;
        }

        Update();
    }

    private void Detach()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.SizeChanged -= OnTopLevelSizeChanged;
        if (_topLevel.InsetsManager is { } insets)
        {
            insets.SafeAreaChanged -= OnSafeAreaChanged;
        }

        if (_topLevel.InputPane is { } inputPane)
        {
            inputPane.StateChanged -= OnInputPaneStateChanged;
        }

        _topLevel = null;
        _keyboard = default;
    }

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) => Update();

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Update();

    private void OnInputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        _keyboard = e.NewState == InputPaneState.Open ? e.EndRect : default;
        Update();
    }

    private void Update()
    {
        if (_topLevel is null)
        {
            return;
        }

        Point? origin = _control.TranslatePoint(default, _topLevel);
        if (origin is null)
        {
            return;
        }

        Rect bounds = new(origin.Value, _control.Bounds.Size);
        Size clientSize = _topLevel.ClientSize;
        Thickness safeArea = _topLevel.InsetsManager?.SafeAreaPadding ?? default;

        double left = Math.Max(0, safeArea.Left - bounds.Left);
        double top = Math.Max(0, safeArea.Top - bounds.Top);
        double right = Math.Max(0, safeArea.Right - (clientSize.Width - bounds.Right));
        double bottom = Math.Max(0, safeArea.Bottom - (clientSize.Height - bounds.Bottom));

        if (_keyboard.Height > 0)
        {
            bottom = Math.Max(bottom, bounds.Bottom - _keyboard.Top);
        }

        Thickness result = new(left, top, right, bottom);
        if (result == _last)
        {
            return;
        }

        _last = result;
        _onChanged(result);
    }
}
