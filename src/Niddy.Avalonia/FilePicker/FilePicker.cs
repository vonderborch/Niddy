using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Niddy.Avalonia.FilePicker;

/// <summary>
///     Thin wrappers around Avalonia's <see cref="IStorageProvider"/>. All methods require a <see cref="Control"/>
///     that is attached to a visual tree so the top level can be resolved.
///     <para>
///         The main methods return <see cref="IStorageFile"/> / <see cref="IStorageFolder"/> handles, which work on
///         every platform: read and write through <see cref="IStorageFile.OpenReadAsync"/> and
///         <see cref="IStorageFile.OpenWriteAsync"/>. Dispose the handles when you're done with them.
///     </para>
///     <para>
///         The <c>*Path</c> methods return local file-system paths instead. They are desktop-only: on Android,
///         iOS, and browser, picked items usually have no local path, so they return null (or skip the item).
///     </para>
/// </summary>
public static class FilePicker
{
    private static IStorageProvider GetStorageProvider(Control source)
    {
        var topLevel = TopLevel.GetTopLevel(source)
            ?? throw new InvalidOperationException("Cannot resolve TopLevel from source control.");
        return topLevel.StorageProvider;
    }

    /// <summary>Opens a single-file picker. Returns the chosen file, or null if cancelled.</summary>
    public static async Task<IStorageFile?> OpenFile(
        Control source,
        string title = "Open File",
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        var results = await GetStorageProvider(source).OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes,
        });

        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>Opens a multi-file picker. Returns the chosen files (empty if cancelled).</summary>
    public static async Task<IReadOnlyList<IStorageFile>> OpenFiles(
        Control source,
        string title = "Open Files",
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        return await GetStorageProvider(source).OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = fileTypes,
        });
    }

    /// <summary>Opens a save-file picker. Returns the file to write to, or null if cancelled.</summary>
    public static async Task<IStorageFile?> SaveAs(
        Control source,
        string title = "Save As",
        string? suggestedFileName = null,
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        return await GetStorageProvider(source).SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypes,
        });
    }

    /// <summary>Opens a single-folder picker. Returns the chosen folder, or null if cancelled.</summary>
    public static async Task<IStorageFolder?> OpenFolder(
        Control source,
        string title = "Select Folder"
    )
    {
        var results = await GetStorageProvider(source).OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>Opens a multi-folder picker. Returns the chosen folders (empty if cancelled).</summary>
    public static async Task<IReadOnlyList<IStorageFolder>> OpenFolders(
        Control source,
        string title = "Select Folders"
    )
    {
        return await GetStorageProvider(source).OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
        });
    }

    /// <summary>
    ///     Desktop-only. Opens a single-file picker and returns the chosen path, or null if cancelled
    ///     or the file has no local path (mobile, browser).
    /// </summary>
    public static async Task<string?> OpenFilePath(
        Control source,
        string title = "Open File",
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        using var file = await OpenFile(source, title, fileTypes);
        return file?.TryGetLocalPath();
    }

    /// <summary>
    ///     Desktop-only. Opens a multi-file picker and returns the chosen paths (empty if cancelled).
    ///     Files without a local path (mobile, browser) are skipped.
    /// </summary>
    public static async Task<IReadOnlyList<string>> OpenFilePaths(
        Control source,
        string title = "Open Files",
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        return ToLocalPaths(await OpenFiles(source, title, fileTypes));
    }

    /// <summary>
    ///     Desktop-only. Opens a save-file picker and returns the chosen path, or null if cancelled
    ///     or the file has no local path (mobile, browser).
    /// </summary>
    public static async Task<string?> SaveAsPath(
        Control source,
        string title = "Save As",
        string? suggestedFileName = null,
        IReadOnlyList<FilePickerFileType>? fileTypes = null
    )
    {
        using var file = await SaveAs(source, title, suggestedFileName, fileTypes);
        return file?.TryGetLocalPath();
    }

    /// <summary>
    ///     Desktop-only. Opens a single-folder picker and returns the chosen path, or null if cancelled
    ///     or the folder has no local path (mobile, browser).
    /// </summary>
    public static async Task<string?> OpenFolderPath(
        Control source,
        string title = "Select Folder"
    )
    {
        using var folder = await OpenFolder(source, title);
        return folder?.TryGetLocalPath();
    }

    /// <summary>
    ///     Desktop-only. Opens a multi-folder picker and returns the chosen paths (empty if cancelled).
    ///     Folders without a local path (mobile, browser) are skipped.
    /// </summary>
    public static async Task<IReadOnlyList<string>> OpenFolderPaths(
        Control source,
        string title = "Select Folders"
    )
    {
        return ToLocalPaths(await OpenFolders(source, title));
    }

    private static List<string> ToLocalPaths(IEnumerable<IStorageItem> items)
    {
        var paths = new List<string>();
        foreach (var item in items)
        {
            using (item)
            {
                if (item.TryGetLocalPath() is { } path)
                    paths.Add(path);
            }
        }
        return paths;
    }
}
