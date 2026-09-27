using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace Niddy.Avalonia.Toast;

/// <summary>
///     A single toast notification, displayed inside a <see cref="ToastHost" />. Dismisses itself after its duration
///     (paused while the pointer is over it), or when clicked.
/// </summary>
public partial class ToastItem : UserControl
{
    private readonly DispatcherTimer _timer;

    private Action? _action;

    private int _count = 1;

    /// <summary>The message shown.</summary>
    internal string Message
    {
        get
        {
            return MessageText.Text ?? string.Empty;
        }
        set
        {
            MessageText.Text = value;
        }
    }

    /// <summary>The style: icon and color.</summary>
    internal ToastType Type
    {
        get;
        set
        {
            field = value;
            (string, Color) tuple = value switch
            {
                ToastType.Success => ("✓", Color.Parse("#2a7d2a")), 
                ToastType.Warning => ("⚠", Color.Parse("#b85c00")), 
                ToastType.Error => ("✕", Color.Parse("#c0392b")), 
                _ => ("ℹ", Color.Parse("#1a6fa8")), 
            };
            var (text, color) = tuple;
            IconText.Text = text;
            IconText.Foreground = Brushes.White;
            MessageText.Foreground = Brushes.White;
            ToastBorder.Background = new SolidColorBrush(color);
        }
    }

    /// <summary>The key duplicates are merged by, or null to never merge.</summary>
    internal string? Key { get; set; }

    /// <summary>How many times this toast has been shown while visible.</summary>
    internal int Count
    {
        get
        {
            return _count;
        }
        set
        {
            _count = value;
            CountText.Text = $"×{value}";
            CountBadge.IsVisible = value > 1;
        }
    }

    /// <summary>How long the toast stays up; zero or less keeps it until dismissed.</summary>
    internal TimeSpan Duration { get; private set; }

    /// <summary>Whether the toast has been dismissed.</summary>
    internal bool IsDismissed { get; private set; }

    /// <summary>The progress shown: null for none, <see cref="double.NaN" /> for indeterminate, otherwise 0 to 1.</summary>
    internal double? Progress
    {
        get
        {
            return ProgressIndicator.IsVisible ? new double?(ProgressIndicator.IsIndeterminate ? double.NaN : ProgressIndicator.Value) : ((double?)null);
        }
        set
        {
            ProgressIndicator.IsVisible = value.HasValue;
            ProgressIndicator.IsIndeterminate = value.HasValue && double.IsNaN(value.GetValueOrDefault());
            if (value.HasValue)
            {
                double valueOrDefault = value.GetValueOrDefault();
                if (!double.IsNaN(valueOrDefault))
                {
                    ProgressIndicator.Value = Math.Clamp(valueOrDefault, 0.0, 1.0);
                }
            }
        }
    }

    /// <summary>The action button's text, or null if there's no button.</summary>
    internal string? ActionText => ActionButton.IsVisible ? (ActionButton.Content as string) : null;

    internal event EventHandler? Dismissed;

    internal ToastItem(string message, ToastType type, TimeSpan duration, string? actionText = null, Action? action = null)
    {
        InitializeComponent();
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) =>
        {
            Dismiss();
        };
        Message = message;
        Type = type;
        SetAction(actionText, action);
        Restart(duration);
    }

    /// <summary>Shows or hides the action button.</summary>
    internal void SetAction(string? text, Action? action)
    {
        _action = action;
        ActionButton.Content = text;
        ActionButton.IsVisible = text != null && action != null;
    }

    /// <summary>Restarts the dismissal countdown with a new duration.</summary>
    internal void Restart(TimeSpan duration)
    {
        Duration = duration;
        _timer.Stop();
        if (duration > TimeSpan.Zero && !IsDismissed)
        {
            _timer.Interval = duration;
            if (!base.IsPointerOver)
            {
                _timer.Start();
            }
        }
    }

    /// <summary>Runs the action, as if its button was clicked, then dismisses the toast.</summary>
    internal void InvokeAction()
    {
        Action action = _action;
        Dismiss();
        action?.Invoke();
    }

    internal void Dismiss()
    {
        if (!IsDismissed)
        {
            IsDismissed = true;
            _timer.Stop();
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && !Progress.HasValue)
        {
            Dismiss();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _timer.Stop();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Restart(Duration);
    }

    private void ActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        InvokeAction();
    }
}
