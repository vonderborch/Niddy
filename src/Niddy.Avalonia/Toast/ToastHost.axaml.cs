using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.Utilities;
using System.Collections.ObjectModel;

namespace Niddy.Avalonia.Toast;

/// <summary>
///     Hosts content and shows toasts on top of it, at any of the eight <see cref="ScreenPlacement"/>s. Toasts are
///     kept clear of safe-area insets and the on-screen keyboard. On narrow screens and in mobile mode they move to
///     <see cref="NarrowPlacement"/> (bottom center by default).
/// </summary>
public partial class ToastHost : UserControl
{
    private const double ToastMargin = 16;

    /// <summary>Defines the <see cref="Placement"/> property.</summary>
    public static readonly StyledProperty<ScreenPlacement> PlacementProperty =
        AvaloniaProperty.Register<ToastHost, ScreenPlacement>(nameof(Placement), ScreenPlacement.BottomRight);

    /// <summary>Defines the <see cref="NarrowPlacement"/> property.</summary>
    public static readonly StyledProperty<ScreenPlacement?> NarrowPlacementProperty =
        AvaloniaProperty.Register<ToastHost, ScreenPlacement?>(nameof(NarrowPlacement), ScreenPlacement.BottomCenter);

    /// <summary>Defines the <see cref="NarrowWidth"/> property.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(NarrowWidth), 600);

    /// <summary>Defines the <see cref="ActualPlacement"/> property.</summary>
    public static readonly DirectProperty<ToastHost, ScreenPlacement> ActualPlacementProperty =
        AvaloniaProperty.RegisterDirect<ToastHost, ScreenPlacement>(nameof(ActualPlacement), host => host.ActualPlacement);

    private readonly ObservableCollection<ToastItem> _toasts = new();
    private readonly Dictionary<ToastItem, ToastHandle> _handles = new();

    /// <summary>Creates an empty toast host.</summary>
    public ToastHost()
    {
        InitializeComponent();
        ToastList.ItemsSource = _toasts;
        SizeChanged += (_, _) => ApplyPlacement();
        _ = new ObstructionTracker(this, obstruction =>
            ToastList.Margin = new Thickness(ToastMargin) + obstruction);
        ApplyPlacement();
    }

    /// <summary>
    ///     Whether showing a toast with the same type and message as one still showing merges them (restarting the
    ///     countdown and showing a count) instead of stacking another. Defaults to true.
    /// </summary>
    public bool MergeDuplicates { get; set; } = true;

    /// <summary>Gets or sets the main content displayed beneath the toast layer.</summary>
    public new object? Content
    {
        get => HostContent.Content;
        set => HostContent.Content = value;
    }

    /// <summary>Where toasts are shown on wide screens. Defaults to <see cref="ScreenPlacement.BottomRight"/>.</summary>
    public ScreenPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    ///     Where toasts are shown in mobile mode or when the host is narrower than <see cref="NarrowWidth"/>. Defaults
    ///     to <see cref="ScreenPlacement.BottomCenter"/>; null always uses <see cref="Placement"/>.
    /// </summary>
    public ScreenPlacement? NarrowPlacement
    {
        get => GetValue(NarrowPlacementProperty);
        set => SetValue(NarrowPlacementProperty, value);
    }

    /// <summary>The host width below which <see cref="NarrowPlacement"/> is used. Defaults to 600.</summary>
    public double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <summary>Where toasts are shown right now: <see cref="Placement"/> or <see cref="NarrowPlacement"/>.</summary>
    public ScreenPlacement ActualPlacement
    {
        get;
        private set => SetAndRaise(ActualPlacementProperty, ref field, value);
    } = ScreenPlacement.BottomRight;

    /// <summary>Set by the app shell, which knows the mode before the host is shown.</summary>
    internal bool IsMobile
    {
        get;
        set
        {
            field = value;
            ApplyPlacement();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlacementProperty || change.Property == NarrowPlacementProperty || change.Property == NarrowWidthProperty)
            ApplyPlacement();
    }

    private void ApplyPlacement()
    {
        var narrow = IsMobile || NiddyApp.CurrentMode == AppMode.Mobile || (Bounds.Width > 0 && Bounds.Width < NarrowWidth);
        var placement = narrow && NarrowPlacement is { } narrowPlacement ? narrowPlacement : Placement;
        ActualPlacement = placement;
        ToastList.HorizontalAlignment = placement.HorizontalAlignment;
        ToastList.VerticalAlignment = placement.VerticalAlignment;
    }

    internal ToastHandle AddToast(ToastItem item)
    {
        // The newest toast is nearest the edge it appears at.
        if (ActualPlacement.IsTop)
            _toasts.Insert(0, item);
        else
            _toasts.Add(item);

        var handle = new ToastHandle(item);
        _handles[item] = handle;
        item.Dismissed += (_, _) =>
        {
            _toasts.Remove(item);
            _handles.Remove(item);
        };
        return handle;
    }

    /// <summary>Finds a visible toast by its merge key.</summary>
    internal ToastHandle? Find(string key) =>
        _handles.FirstOrDefault(h => h.Key.Key == key && !h.Key.IsDismissed).Value;

    /// <summary>The toasts showing, newest nearest the edge.</summary>
    internal IReadOnlyList<ToastItem> Toasts => _toasts;

    /// <summary>
    ///     Walks the logical parent chain from <paramref name="control"/> upward to find
    ///     the nearest <see cref="ToastHost"/>. Returns null if none is found.
    /// </summary>
    public static ToastHost? FindInVisualTree(Control control)
    {
        var current = control.Parent;
        while (current is not null)
        {
            if (current is ToastHost host)
                return host;
            current = current.Parent;
        }
        return null;
    }
}
