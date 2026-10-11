namespace Niddy.Core.Web;

/// <summary>
/// Options for <see cref="WebContent.DownloadFileAsync"/>, <see cref="WebContent.DownloadToFolderAsync"/> and their
/// synchronous versions.
/// </summary>
public sealed record DownloadOptions
{
    internal static readonly DownloadOptions Default = new();

    /// <summary>
    /// Replace a file that's already there. Without it, an existing file is a <see cref="WebContent.ErrorCodes.FileExists"/>
    /// failure. <see cref="WebContent.DownloadFileAsync"/> checks before it sends anything;
    /// <see cref="WebContent.DownloadToFolderAsync"/> can only check once the response says what the file is called.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Told how far the download has got: once when it starts, about every 100 ms while it runs, and once at the end.
    /// A <see cref="Progress{T}"/> created on the UI thread reports there.
    /// </summary>
    public IProgress<DownloadProgress>? Progress { get; init; }

    /// <summary>
    /// The most bytes to accept. A larger <c>Content-Length</c> fails before anything is read, and a body that runs
    /// past it (the length can be missing or wrong) fails as soon as it does. Either way the result is a
    /// <see cref="WebContent.ErrorCodes.TooLarge"/> failure and nothing is saved. Null is no limit. Set it for URLs you
    /// don't control, so a misbehaving server can't fill the disk.
    /// </summary>
    public long? MaxBytes { get; init; }

    /// <summary>
    /// The file's expected SHA-256 hash: 64 hex characters, in either case, optionally after <c>sha256:</c> (the form
    /// GitHub's API uses). It's computed as the file downloads, and a mismatch is a
    /// <see cref="WebContent.ErrorCodes.ChecksumMismatch"/> failure that saves nothing. Null skips the check.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Keep what arrived when a download fails partway, and carry on from there next time instead of starting over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The partial download is kept as a hidden <c>.{id}.download</c> file in the target folder, named after the URL,
    /// with a <c>.validator</c> file beside it holding the server's <c>ETag</c> or <c>Last-Modified</c>. The next
    /// download of the same URL asks only for the rest (an HTTP <c>Range</c> request with <c>If-Range</c>). If the
    /// server doesn't support ranges, sent neither header, or the file has changed since, the download starts over.
    /// Both files are removed once the download finishes, and also after a <see cref="Sha256"/> mismatch or a
    /// <see cref="MaxBytes"/> failure, where the partial data can't be trusted.
    /// </para>
    /// <para>
    /// The finished file still only appears once the whole download has arrived. Worth it for large files; for small
    /// ones it's simpler to retry from the start.
    /// </para>
    /// </remarks>
    public bool Resume { get; init; }
}
