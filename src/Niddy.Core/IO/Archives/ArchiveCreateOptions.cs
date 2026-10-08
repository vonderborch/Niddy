using System.IO.Compression;

namespace Niddy.Core.IO.Archives;

/// <summary>Options for <see cref="Archive.Create(string, string, ArchiveCreateOptions?)"/>.</summary>
public sealed record ArchiveCreateOptions
{
    /// <summary>
    /// The format. Null takes it from the archive path's extension (<c>.zip</c>, <c>.tar</c>, <c>.tar.gz</c>,
    /// <c>.tgz</c>). Required when writing to a stream.
    /// </summary>
    public ArchiveFormat? Format { get; init; }

    /// <summary>How hard to compress. Applies to <see cref="ArchiveFormat.Zip"/> and <see cref="ArchiveFormat.TarGz"/>.</summary>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>
    /// For a folder, put its contents under a top-level folder of the same name (<c>data/a.txt</c>) instead of at
    /// the root of the archive (<c>a.txt</c>). Ignored for a single file.
    /// </summary>
    public bool IncludeBaseDirectory { get; init; }

    /// <summary>Replace an existing archive. Without it, an existing archive is an <see cref="IOException"/>.</summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Folders to leave out, with everything in them, as <c>.gitignore</c>-style patterns relative to the source folder
    /// (see <c>Path.IsInList</c>): <c>"bin"</c> is every folder named <c>bin</c>, <c>"/bin"</c> only the top-level
    /// one, and <c>"docs/drafts"</c> that folder only.
    /// </summary>
    public IReadOnlyList<string>? ExcludedDirectories { get; init; }

    /// <summary>Files to leave out, matched like <see cref="ExcludedDirectories"/>: <c>"*.log"</c> is every
    /// log file and <c>".DS_Store"</c> every file named <c>.DS_Store</c>.</summary>
    public IReadOnlyList<string>? ExcludedFiles { get; init; }

    /// <summary>
    /// Receives progress after each entry and after every MiB within one. <see cref="Progress{T}"/> posts each
    /// report to the context it was created on, so reports arrive asynchronously.
    /// </summary>
    public IProgress<ArchiveProgress>? Progress { get; init; }
}
