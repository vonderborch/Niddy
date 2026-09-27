using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Dock.Controls.ProportionalStackPanel;

namespace Niddy.Avalonia.State;

/// <summary>
/// Remembers the sizes users give to resizable layouts and restores them next time: the column widths and row heights
/// of a <see cref="Grid"/> with <see cref="GridSplitter"/>s, or the proportions of a <see cref="ProportionalStackPanel"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="UiStateStore.Default"/>. Saved sizes are only restored if the number of columns, rows or panes still
/// matches, so changing the layout in a new version doesn't break it.
/// </remarks>
/// <example>
/// <code>
/// &lt;Grid ColumnDefinitions="250,4,*" state:LayoutMemory.Key="MainSplit"&gt;
///     &lt;TreeView /&gt;
///     &lt;GridSplitter Grid.Column="1" /&gt;
///     &lt;ContentControl Grid.Column="2" /&gt;
/// &lt;/Grid&gt;
/// </code>
/// </example>
public static class LayoutMemory
{
    /// <summary>Defines the <c>Key</c> attached property.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(LayoutMemory));

    private static readonly AttachedProperty<IDisposable?> RegistrationProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("Registration", typeof(LayoutMemory));

    static LayoutMemory()
    {
        KeyProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.GetValue(RegistrationProperty)?.Dispose();
            control.SetValue(RegistrationProperty, null);
            if (e.NewValue is not string { Length: > 0 } key)
                return;

            // Children and definitions may not exist yet while AXAML is loading.
            if (control.IsLoaded)
                control.SetValue(RegistrationProperty, Attach(control, key));
            else
                control.Loaded += OnLoaded;

            void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs args)
            {
                control.Loaded -= OnLoaded;
                if (GetKey(control) == key)
                    control.SetValue(RegistrationProperty, Attach(control, key));
            }
        });
    }

    /// <summary>Gets the key a layout's sizes are saved under.</summary>
    public static string? GetKey(Control control) => control.GetValue(KeyProperty);

    /// <summary>Sets the key a layout's sizes are saved under. Supported on <see cref="Grid"/> and <see cref="ProportionalStackPanel"/>.</summary>
    public static void SetKey(Control control, string? key) => control.SetValue(KeyProperty, key);

    /// <summary>Restores a layout's saved sizes and saves them as they change.</summary>
    /// <param name="control">A <see cref="Grid"/> or <see cref="ProportionalStackPanel"/>.</param>
    /// <param name="key">The key to save the sizes under, unique in the app.</param>
    /// <param name="store">The store; defaults to <see cref="UiStateStore.Default"/>. If there's none, nothing happens.</param>
    /// <returns>Stops remembering the layout when disposed.</returns>
    public static IDisposable Attach(Control control, string key, UiStateStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        store ??= UiStateStore.Default;
        return (control, store) switch
        {
            (_, null) => new Subscriptions(),
            (Grid grid, _) => AttachGrid(grid, key, store),
            (ProportionalStackPanel panel, _) => AttachPanel(panel, key, store),
            _ => throw new NotSupportedException($"{nameof(LayoutMemory)} supports Grid and ProportionalStackPanel, not {control.GetType().Name}."),
        };
    }

    private static IDisposable AttachGrid(Grid grid, string key, UiStateStore store)
    {
        var columnsKey = key + "/columns";
        var rowsKey = key + "/rows";
        Restore(store.GetLayout(columnsKey), grid.ColumnDefinitions, ColumnDefinition.WidthProperty);
        Restore(store.GetLayout(rowsKey), grid.RowDefinitions, RowDefinition.HeightProperty);

        var subscriptions = new Subscriptions();
        Watch(grid.ColumnDefinitions, ColumnDefinition.WidthProperty, columnsKey);
        Watch(grid.RowDefinitions, RowDefinition.HeightProperty, rowsKey);
        return subscriptions;

        void Watch<T>(IList<T> definitions, StyledProperty<GridLength> property, string layoutKey) where T : DefinitionBase
        {
            if (definitions.Count == 0)
                return;
            foreach (var definition in definitions)
                subscriptions.Add(definition, property, () =>
                    store.SetLayout(layoutKey, definitions.Select(d => d.GetValue(property).ToString()).ToArray()));
        }
    }

    private static void Restore<T>(IReadOnlyList<string>? saved, IList<T> definitions, StyledProperty<GridLength> property) where T : DefinitionBase
    {
        if (saved is null || saved.Count != definitions.Count)
            return;
        var lengths = new GridLength[saved.Count];
        for (var i = 0; i < saved.Count; i++)
        {
            try
            {
                lengths[i] = GridLength.Parse(saved[i]);
            }
            catch (FormatException)
            {
                return;
            }
        }
        for (var i = 0; i < lengths.Length; i++)
            definitions[i].SetValue(property, lengths[i]);
    }

    private static IDisposable AttachPanel(ProportionalStackPanel panel, string key, UiStateStore store)
    {
        var panes = panel.Children.Where(c => c is not ProportionalStackPanelSplitter).ToList();
        if (store.GetLayout(key) is { } saved && saved.Count == panes.Count)
        {
            var proportions = saved.Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToArray();
            if (proportions.All(p => double.IsFinite(p) && p >= 0))
                for (var i = 0; i < panes.Count; i++)
                    ProportionalStackPanel.SetProportion(panes[i], proportions[i]);
        }

        var subscriptions = new Subscriptions();
        foreach (var pane in panes)
            subscriptions.Add(pane, ProportionalStackPanel.ProportionProperty, () =>
            {
                var proportions = panes.Select(ProportionalStackPanel.GetProportion).ToArray();
                if (proportions.All(double.IsFinite))
                    store.SetLayout(key, proportions.Select(p => p.ToString("R", CultureInfo.InvariantCulture)).ToArray());
            });
        return subscriptions;
    }

    private sealed class Subscriptions : IDisposable
    {
        private readonly List<Action> _unsubscribe = [];

        public void Add(AvaloniaObject target, AvaloniaProperty property, Action changed)
        {
            void Handler(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == property)
                    changed();
            }

            target.PropertyChanged += Handler;
            _unsubscribe.Add(() => target.PropertyChanged -= Handler);
        }

        public void Dispose()
        {
            foreach (var unsubscribe in _unsubscribe)
                unsubscribe();
            _unsubscribe.Clear();
        }
    }
}
