namespace Niddy.Core.IO.Archives;

/// <summary>Options for <see cref="Archive.Extract(string, string, ArchiveExtractOptions?)"/>.</summary>
public sealed record ArchiveExtractOptions
{
    /// <summary>
    /// The format. Null takes it from the archive path's extension, or else from the archive's first bytes (a stream
    /// must be seekable for that).
    /// </summary>
    public ArchiveFormat? Format { get; init; }

    /// <summary>Replace files that already exist. Without it, an existing file is an <see cref="IOException"/>.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Extract only the entries this returns true for. Null extracts everything.</summary>
    public Func<ArchiveEntryInfo, bool>? Filter { get; init; }

    /// <summary>
    /// The most entries to extract; more is an <see cref="InvalidDataException"/>. Null is no limit. Set it for
    /// archives you didn't make.
    /// </summary>
    public int? MaxEntries { get; init; }

    /// <summary>
    /// The most uncompressed bytes to write, counted as they're written rather than trusted from the archive's
    /// headers; more is an <see cref="InvalidDataException"/>. Null is no limit. Set it for archives you didn't make,
    /// so a "zip bomb" can't fill the disk.
    /// </summary>
    public long? MaxTotalBytes { get; init; }

    /// <summary>
    /// Receives progress after each entry and after every MiB within one. <see cref="Progress{T}"/> posts each
    /// report to the context it was created on, so reports arrive asynchronously.
    /// </summary>
    public IProgress<ArchiveProgress>? Progress { get; init; }
}
