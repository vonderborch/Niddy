using Avalonia.Controls;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A column of <see cref="Dialog.Table"/>: a header and how to get each row's value.</summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Header">The column header.</param>
/// <param name="Value">Gets a row's value, shown with <see cref="object.ToString"/> and used for sorting.</param>
public sealed record TableColumn<T>(string Header, Func<T, object?> Value)
{
    /// <summary>Formats the value for display; defaults to <see cref="object.ToString"/>.</summary>
    public Func<object?, string>? Format { get; init; }

    /// <summary>Right-aligns the column, e.g. for numbers. Defaults to false.</summary>
    public bool AlignRight { get; init; }

    /// <summary>The column width, e.g. <c>new DataGridLength(1, DataGridLengthUnitType.Star)</c>. Defaults to fitting the content.</summary>
    public DataGridLength? Width { get; init; }
}
