using Avalonia.Controls;

namespace Niddy.Avalonia.Tables;

/// <summary>The pager a <see cref="TableGrid"/> adds under its rows. The grid wires it up and sets its state.</summary>
internal sealed partial class TableGridPager : UserControl
{
    public TableGridPager()
    {
        InitializeComponent();
    }
}
