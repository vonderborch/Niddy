namespace Niddy.Avalonia.Tables;

/// <summary>The row of a <see cref="TableGrid.ItemActivated"/> event.</summary>
/// <param name="item">The row's item.</param>
public sealed class TableGridItemEventArgs(object item) : EventArgs
{
    /// <summary>The row's item.</summary>
    public object Item { get; } = item;
}
