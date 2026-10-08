using System.Buffers;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.IO.Archives;

/// <summary>
/// Creates, extracts and reads zip, tar and tar.gz archives: archive a file or folder, extract all or some of an
/// archive, or extract, read or stream a single entry.
/// </summary>
/// <remarks>
/// <para>
/// Every operation takes the archive as a path or as a <see cref="Stream"/> (left open), and has an <c>…Async</c>
/// form. The format comes from the argument, else the path's extension, else (when reading) the archive's first bytes.
/// </para>
/// <para>
/// Entry names use <c>/</c>. When looking an entry up, <c>\</c> counts as <c>/</c> and a leading <c>./</c> or
/// <c>/</c> is ignored, so <c>docs\a.txt</c>, <c>./docs/a.txt</c> and <c>docs/a.txt</c> all find the same entry.
/// </para>
/// <para>
/// Extracting never writes outside the destination folder: an entry whose name is absolute or climbs out with
/// <c>..</c>, or a tar link pointing outside, stops the extract with an <see cref="IOException"/>. For archives you
/// didn't make, also set <see cref="ArchiveExtractOptions.MaxEntries"/> and
/// <see cref="ArchiveExtractOptions.MaxTotalBytes"/> (or <c>maxBytes</c> for single entries).
/// </para>
/// <para>
/// Zip archives have a table of contents, so reading one entry is cheap. Tar archives don't: finding an entry reads
/// the archive from the start until it turns up.
/// </para>
/// </remarks>
public static class Archive
{
    private const int BufferSize = 81920;
    private const long ProgressStep = 1024 * 1024;

    private const UnixFileMode DefaultDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// Gets the archive format a path's extension names: <c>.zip</c>, <c>.tar</c>, or <c>.tar.gz</c> / <c>.tgz</c>,
    /// in any case.
    /// </summary>
    /// <param name="path">A file name or path.</param>
    /// <returns>The format, or null for any other extension.</returns>
    public static ArchiveFormat? FormatFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string name = Path.GetFileName(path);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.Zip;
        }

        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.TarGz;
        }

        if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.Tar;
        }

        return null;
    }

    // ---- Create ----

    /// <summary>
    /// Archives a file or folder. The archive is written to a temporary file and moved into place, so a failed or
    /// cancelled run never leaves a partial archive behind.
    /// </summary>
    /// <remarks>
    /// A folder's files and subfolders (empty ones included) are added with paths relative to it, in ordinal order.
    /// Hidden files are included. A symbolic link to a file is archived as the file's contents; a linked folder is
    /// skipped, which also avoids loops. On Linux and macOS, Unix permissions are stored.
    /// </remarks>
    /// <param name="source">The file or folder to archive.</param>
    /// <param name="archivePath">The archive to write. Its folder is created if missing.</param>
    /// <param name="options">The format, compression, exclusions and progress. Null uses the defaults and takes the
    /// format from <paramref name="archivePath"/>'s extension.</param>
    /// <exception cref="ArgumentException">The format isn't set and <paramref name="archivePath"/>'s extension isn't
    /// one of those <see cref="FormatFromPath"/> knows, or <paramref name="archivePath"/> is inside
    /// <paramref name="source"/>.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="source"/> doesn't exist.</exception>
    /// <exception cref="IOException">The archive exists and <see cref="ArchiveCreateOptions.Overwrite"/> isn't
    /// set.</exception>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
    /// </exception>
    public static void Create(string source, string archivePath, ArchiveCreateOptions? options = null) =>
        CreateCore(source, archivePath, options ?? new ArchiveCreateOptions(), useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="Create(string, string, ArchiveCreateOptions?)"/>
    public static Task CreateAsync(string source, string archivePath, ArchiveCreateOptions? options = null, CancellationToken cancellationToken = default) =>
        CreateCore(source, archivePath, options ?? new ArchiveCreateOptions(), useAsync: true, cancellationToken).AsTask();

    /// <summary>
    /// Archives a file or folder to a stream, which is left open. <see cref="ArchiveCreateOptions.Format"/> is
    /// required. If this fails part-way, the stream holds a partial archive.
    /// </summary>
    /// <param name="source">The file or folder to archive.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="options">The format (required), compression, exclusions and progress.</param>
    /// <exception cref="ArgumentException"><see cref="ArchiveCreateOptions.Format"/> isn't set, or
    /// <paramref name="destination"/> can't be written.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="source"/> doesn't exist.</exception>
    public static void Create(string source, Stream destination, ArchiveCreateOptions options) =>
        CreateCore(source, destination, options, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="Create(string, Stream, ArchiveCreateOptions)"/>
    public static Task CreateAsync(string source, Stream destination, ArchiveCreateOptions options, CancellationToken cancellationToken = default) =>
        CreateCore(source, destination, options, useAsync: true, cancellationToken).AsTask();

    // ---- Extract ----

    /// <summary>
    /// Extracts an archive, or the entries <see cref="ArchiveExtractOptions.Filter"/> accepts, into a folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <paramref name="destinationDirectory"/> doesn't exist, the archive is extracted into a temporary folder next
    /// to it, which is moved into place at the end, so a failure leaves nothing behind. Extracting into a folder that
    /// already exists isn't atomic: files written before a failure stay.
    /// </para>
    /// <para>
    /// Files keep their last-write times, and on Linux and macOS their permissions (the owner can always read and
    /// write). Tar symbolic links are recreated when they point inside the destination (on Windows that needs
    /// Developer Mode or admin rights); hard links are extracted as copies. Device and FIFO entries are skipped.
    /// </para>
    /// </remarks>
    /// <param name="archivePath">The archive.</param>
    /// <param name="destinationDirectory">The folder to extract into. Created if missing.</param>
    /// <param name="options">The format, overwriting, filter, limits and progress. Null uses the defaults.</param>
    /// <exception cref="ArgumentException">The format isn't given and can't be detected from a stream that can't
    /// seek.</exception>
    /// <exception cref="InvalidDataException">The archive is corrupt or not a zip, tar or tar.gz, or it goes over
    /// <see cref="ArchiveExtractOptions.MaxEntries"/> or <see cref="ArchiveExtractOptions.MaxTotalBytes"/>.</exception>
    /// <exception cref="IOException">An entry would land outside the destination, or a file exists and
    /// <see cref="ArchiveExtractOptions.Overwrite"/> isn't set.</exception>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
    /// </exception>
    public static void Extract(string archivePath, string destinationDirectory, ArchiveExtractOptions? options = null) =>
        ExtractCore(ArchiveSource.From(archivePath), destinationDirectory, options, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="Extract(string, string, ArchiveExtractOptions?)"/>
    public static void Extract(Stream archive, string destinationDirectory, ArchiveExtractOptions? options = null) =>
        ExtractCore(ArchiveSource.From(archive), destinationDirectory, options, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="Extract(string, string, ArchiveExtractOptions?)"/>
    public static Task ExtractAsync(string archivePath, string destinationDirectory, ArchiveExtractOptions? options = null, CancellationToken cancellationToken = default) =>
        ExtractCore(ArchiveSource.From(archivePath), destinationDirectory, options, useAsync: true, cancellationToken).AsTask();

    /// <inheritdoc cref="Extract(string, string, ArchiveExtractOptions?)"/>
    public static Task ExtractAsync(Stream archive, string destinationDirectory, ArchiveExtractOptions? options = null, CancellationToken cancellationToken = default) =>
        ExtractCore(ArchiveSource.From(archive), destinationDirectory, options, useAsync: true, cancellationToken).AsTask();

    // ---- ExtractEntry ----

    /// <summary>
    /// Extracts one file from an archive. The file is written to a temporary file and moved into place, and keeps the
    /// entry's last-write time.
    /// </summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive, e.g. <c>docs/readme.md</c>.</param>
    /// <param name="destinationFile">Where to write it. Its folder is created if missing.</param>
    /// <param name="overwrite">Replace <paramref name="destinationFile"/> if it exists.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <param name="maxBytes">The most bytes to extract; more is an <see cref="InvalidDataException"/>. Null is no
    /// limit.</param>
    /// <exception cref="FileNotFoundException">The archive has no file named <paramref name="entryName"/>.</exception>
    /// <exception cref="IOException"><paramref name="destinationFile"/> exists and <paramref name="overwrite"/> is
    /// false.</exception>
    /// <exception cref="InvalidDataException">The archive is corrupt, or the file is bigger than
    /// <paramref name="maxBytes"/>.</exception>
    public static void ExtractEntry(string archivePath, string entryName, string destinationFile, bool overwrite = false, ArchiveFormat? format = null, long? maxBytes = null) =>
        ExtractEntryCore(ArchiveSource.From(archivePath), entryName, destinationFile, overwrite, format, maxBytes, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="ExtractEntry(string, string, string, bool, ArchiveFormat?, long?)"/>
    public static void ExtractEntry(Stream archive, string entryName, string destinationFile, bool overwrite = false, ArchiveFormat? format = null, long? maxBytes = null) =>
        ExtractEntryCore(ArchiveSource.From(archive), entryName, destinationFile, overwrite, format, maxBytes, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="ExtractEntry(string, string, string, bool, ArchiveFormat?, long?)"/>
    public static Task ExtractEntryAsync(string archivePath, string entryName, string destinationFile, bool overwrite = false, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        ExtractEntryCore(ArchiveSource.From(archivePath), entryName, destinationFile, overwrite, format, maxBytes, useAsync: true, cancellationToken).AsTask();

    /// <inheritdoc cref="ExtractEntry(string, string, string, bool, ArchiveFormat?, long?)"/>
    public static Task ExtractEntryAsync(Stream archive, string entryName, string destinationFile, bool overwrite = false, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        ExtractEntryCore(ArchiveSource.From(archive), entryName, destinationFile, overwrite, format, maxBytes, useAsync: true, cancellationToken).AsTask();

    // ---- OpenEntry / TryOpenEntry ----

    /// <summary>
    /// Opens one file in an archive for reading, without extracting it. Dispose the stream to close the archive.
    /// </summary>
    /// <remarks>
    /// The stream reads forwards only and has no <see cref="Stream.Length"/>; the size is in
    /// <see cref="GetEntries(string, ArchiveFormat?)"/>. For a stream argument, don't touch it until the returned
    /// stream is disposed.
    /// </remarks>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive, e.g. <c>docs/readme.md</c>.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <param name="maxBytes">The most bytes to read; reading past it throws <see cref="InvalidDataException"/>. Null
    /// is no limit.</param>
    /// <returns>A read-only stream of the file's contents.</returns>
    /// <exception cref="FileNotFoundException">The archive has no file named <paramref name="entryName"/>.
    /// Directories and links don't count.</exception>
    /// <exception cref="InvalidDataException">The archive is corrupt or not a zip, tar or tar.gz.</exception>
    public static Stream OpenEntry(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null) =>
        Required(TryOpenEntry(archivePath, entryName, format, maxBytes), entryName);

    /// <inheritdoc cref="OpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static Stream OpenEntry(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null) =>
        Required(TryOpenEntry(archive, entryName, format, maxBytes), entryName);

    /// <inheritdoc cref="OpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static async Task<Stream> OpenEntryAsync(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        Required(await TryOpenEntryAsync(archivePath, entryName, format, maxBytes, cancellationToken), entryName);

    /// <inheritdoc cref="OpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static async Task<Stream> OpenEntryAsync(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        Required(await TryOpenEntryAsync(archive, entryName, format, maxBytes, cancellationToken), entryName);

    /// <summary>
    /// Like <see cref="OpenEntry(string, string, ArchiveFormat?, long?)"/>, but returns null when the archive has no
    /// file named <paramref name="entryName"/>.
    /// </summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <param name="maxBytes">The most bytes to read. Null is no limit.</param>
    /// <returns>A read-only stream of the file's contents, or null.</returns>
    public static Stream? TryOpenEntry(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null) =>
        OpenEntryCore(ArchiveSource.From(archivePath), entryName, format, maxBytes, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="TryOpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static Stream? TryOpenEntry(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null) =>
        OpenEntryCore(ArchiveSource.From(archive), entryName, format, maxBytes, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="TryOpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static async Task<Stream?> TryOpenEntryAsync(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        await OpenEntryCore(ArchiveSource.From(archivePath), entryName, format, maxBytes, useAsync: true, cancellationToken);

    /// <inheritdoc cref="TryOpenEntry(string, string, ArchiveFormat?, long?)"/>
    public static async Task<Stream?> TryOpenEntryAsync(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default) =>
        await OpenEntryCore(ArchiveSource.From(archive), entryName, format, maxBytes, useAsync: true, cancellationToken);

    // ---- ReadEntryBytes / ReadEntryText ----

    /// <summary>Reads one file in an archive into memory.</summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <param name="maxBytes">The most bytes to read; more is an <see cref="InvalidDataException"/>. Null is no
    /// limit.</param>
    /// <returns>The file's contents.</returns>
    /// <exception cref="FileNotFoundException">The archive has no file named <paramref name="entryName"/>.</exception>
    /// <exception cref="InvalidDataException">The archive is corrupt, or the file is bigger than
    /// <paramref name="maxBytes"/>.</exception>
    public static byte[] ReadEntryBytes(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null)
    {
        using Stream stream = OpenEntry(archivePath, entryName, format, maxBytes);
        return stream.ReadToBytes();
    }

    /// <inheritdoc cref="ReadEntryBytes(string, string, ArchiveFormat?, long?)"/>
    public static byte[] ReadEntryBytes(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null)
    {
        using Stream stream = OpenEntry(archive, entryName, format, maxBytes);
        return stream.ReadToBytes();
    }

    /// <inheritdoc cref="ReadEntryBytes(string, string, ArchiveFormat?, long?)"/>
    public static async Task<byte[]> ReadEntryBytesAsync(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await OpenEntryAsync(archivePath, entryName, format, maxBytes, cancellationToken);
        return await stream.ReadToBytesAsync(cancellationToken);
    }

    /// <inheritdoc cref="ReadEntryBytes(string, string, ArchiveFormat?, long?)"/>
    public static async Task<byte[]> ReadEntryBytesAsync(Stream archive, string entryName, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await OpenEntryAsync(archive, entryName, format, maxBytes, cancellationToken);
        return await stream.ReadToBytesAsync(cancellationToken);
    }

    /// <summary>Reads one file in an archive as text.</summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive.</param>
    /// <param name="encoding">The text's encoding. Null is UTF-8.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <param name="maxBytes">The most bytes to read; more is an <see cref="InvalidDataException"/>. Null is no
    /// limit.</param>
    /// <returns>The file's text.</returns>
    /// <exception cref="FileNotFoundException">The archive has no file named <paramref name="entryName"/>.</exception>
    /// <exception cref="InvalidDataException">The archive is corrupt, or the file is bigger than
    /// <paramref name="maxBytes"/>.</exception>
    public static string ReadEntryText(string archivePath, string entryName, Encoding? encoding = null, ArchiveFormat? format = null, long? maxBytes = null)
    {
        using Stream stream = OpenEntry(archivePath, entryName, format, maxBytes);
        return stream.ReadToString(encoding);
    }

    /// <inheritdoc cref="ReadEntryText(string, string, Encoding?, ArchiveFormat?, long?)"/>
    public static string ReadEntryText(Stream archive, string entryName, Encoding? encoding = null, ArchiveFormat? format = null, long? maxBytes = null)
    {
        using Stream stream = OpenEntry(archive, entryName, format, maxBytes);
        return stream.ReadToString(encoding);
    }

    /// <inheritdoc cref="ReadEntryText(string, string, Encoding?, ArchiveFormat?, long?)"/>
    public static async Task<string> ReadEntryTextAsync(string archivePath, string entryName, Encoding? encoding = null, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await OpenEntryAsync(archivePath, entryName, format, maxBytes, cancellationToken);
        return await stream.ReadToStringAsync(encoding, cancellationToken);
    }

    /// <inheritdoc cref="ReadEntryText(string, string, Encoding?, ArchiveFormat?, long?)"/>
    public static async Task<string> ReadEntryTextAsync(Stream archive, string entryName, Encoding? encoding = null, ArchiveFormat? format = null, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await OpenEntryAsync(archive, entryName, format, maxBytes, cancellationToken);
        return await stream.ReadToStringAsync(encoding, cancellationToken);
    }

    // ---- ContainsEntry ----

    /// <summary>Whether an archive has a file named <paramref name="entryName"/>. Directories and links don't count.</summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="entryName">The file's path in the archive.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <exception cref="InvalidDataException">The archive is corrupt or not a zip, tar or tar.gz.</exception>
    public static bool ContainsEntry(string archivePath, string entryName, ArchiveFormat? format = null) =>
        ContainsEntryCore(ArchiveSource.From(archivePath), entryName, format, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="ContainsEntry(string, string, ArchiveFormat?)"/>
    public static bool ContainsEntry(Stream archive, string entryName, ArchiveFormat? format = null) =>
        ContainsEntryCore(ArchiveSource.From(archive), entryName, format, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="ContainsEntry(string, string, ArchiveFormat?)"/>
    public static Task<bool> ContainsEntryAsync(string archivePath, string entryName, ArchiveFormat? format = null, CancellationToken cancellationToken = default) =>
        ContainsEntryCore(ArchiveSource.From(archivePath), entryName, format, useAsync: true, cancellationToken).AsTask();

    /// <inheritdoc cref="ContainsEntry(string, string, ArchiveFormat?)"/>
    public static Task<bool> ContainsEntryAsync(Stream archive, string entryName, ArchiveFormat? format = null, CancellationToken cancellationToken = default) =>
        ContainsEntryCore(ArchiveSource.From(archive), entryName, format, useAsync: true, cancellationToken).AsTask();

    // ---- GetEntries ----

    /// <summary>Lists the files and directories in an archive, in archive order. Links and special entries are left out.</summary>
    /// <param name="archivePath">The archive.</param>
    /// <param name="format">The format. Null detects it.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="InvalidDataException">The archive is corrupt or not a zip, tar or tar.gz.</exception>
    public static IReadOnlyList<ArchiveEntryInfo> GetEntries(string archivePath, ArchiveFormat? format = null) =>
        GetEntriesCore(ArchiveSource.From(archivePath), format, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetEntries(string, ArchiveFormat?)"/>
    public static IReadOnlyList<ArchiveEntryInfo> GetEntries(Stream archive, ArchiveFormat? format = null) =>
        GetEntriesCore(ArchiveSource.From(archive), format, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetEntries(string, ArchiveFormat?)"/>
    public static Task<IReadOnlyList<ArchiveEntryInfo>> GetEntriesAsync(string archivePath, ArchiveFormat? format = null, CancellationToken cancellationToken = default) =>
        GetEntriesCore(ArchiveSource.From(archivePath), format, useAsync: true, cancellationToken).AsTask();

    /// <inheritdoc cref="GetEntries(string, ArchiveFormat?)"/>
    public static Task<IReadOnlyList<ArchiveEntryInfo>> GetEntriesAsync(Stream archive, ArchiveFormat? format = null, CancellationToken cancellationToken = default) =>
        GetEntriesCore(ArchiveSource.From(archive), format, useAsync: true, cancellationToken).AsTask();

    // ==== Internals ====
    // Each operation has one ValueTask core taking useAsync. With useAsync false it only calls synchronous APIs, so the
    // returned ValueTask has already completed and the sync methods can safely call GetAwaiter().GetResult().

    /// <summary>Writes <paramref name="source"/> as an archive to <paramref name="output"/>. Used by
    /// <c>Directory.Backup</c>.</summary>
    internal static void WriteArchive(string source, Stream output, ArchiveCreateOptions options)
    {
        ArchiveFormat format = options.Format ?? throw new ArgumentException("The archive format must be set.", nameof(options));
        List<SourceItem> items = CollectSources(source, options);
        WriteItems(items, output, format, options, useAsync: false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Normalizes an entry name for lookups: <c>/</c> separators, no leading <c>./</c> or <c>/</c>, no
    /// trailing <c>/</c>.</summary>
    internal static string NormalizeName(string name)
    {
        string normalized = name.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..].TrimStart('/');
        }

        return normalized.Trim('/');
    }

    /// <summary>Detects the format from an archive's first bytes (at least 262 for tar).</summary>
    internal static ArchiveFormat? Sniff(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 'P' && header[1] == 'K'
            && ((header[2] == 3 && header[3] == 4) || (header[2] == 5 && header[3] == 6)))
        {
            return ArchiveFormat.Zip;
        }

        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
        {
            return ArchiveFormat.TarGz;
        }

        if (header.Length >= 262 && header.Slice(257, 5).SequenceEqual("ustar"u8))
        {
            return ArchiveFormat.Tar;
        }

        return null;
    }

    private static Stream Required(Stream? stream, string entryName) =>
        stream ?? throw new FileNotFoundException($"The archive has no file named '{entryName}'.", entryName);

    private static async ValueTask CreateCore(string source, string archivePath, ArchiveCreateOptions options, bool useAsync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(options);
        ArchiveFormat format = options.Format ?? FormatFromPath(archivePath)
            ?? throw new ArgumentException($"Can't tell the archive format from '{archivePath}'. Set ArchiveCreateOptions.Format.", nameof(archivePath));

        string fullSource = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        string fullArchive = Path.GetFullPath(archivePath);
        if (FileSystemExtensions.IsSameOrInside(fullArchive, fullSource, Path.Comparison))
        {
            throw new ArgumentException("The archive can't go inside what's being archived.", nameof(archivePath));
        }

        List<SourceItem> items = CollectSources(fullSource, options);
        if (!options.Overwrite && (File.Exists(fullArchive) || Directory.Exists(fullArchive)))
        {
            throw new IOException($"'{fullArchive}' already exists.");
        }

        if (useAsync)
        {
            await AtomicFile.WriteAsync(fullArchive, (stream, ct) => WriteItems(items, stream, format, options, useAsync: true, ct).AsTask(), cancellationToken);
        }
        else
        {
            AtomicFile.Write(fullArchive, stream => WriteItems(items, stream, format, options, useAsync: false, CancellationToken.None).GetAwaiter().GetResult());
        }
    }

    private static async ValueTask CreateCore(string source, Stream destination, ArchiveCreateOptions options, bool useAsync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        ArchiveFormat format = options.Format
            ?? throw new ArgumentException("Set ArchiveCreateOptions.Format when writing to a stream.", nameof(options));
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The stream can't be written to.", nameof(destination));
        }

        List<SourceItem> items = CollectSources(source, options);
        await WriteItems(items, destination, format, options, useAsync, cancellationToken);
    }

    private sealed record SourceItem(string FullPath, string EntryName, bool IsDirectory, long Length, DateTime LastWriteUtc);

    private static List<SourceItem> CollectSources(string source, ArchiveCreateOptions options)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        if (File.Exists(root))
        {
            FileInfo file = new(root);
            return [new SourceItem(root, file.Name, false, file.Length, file.LastWriteTimeUtc)];
        }

        if (!Directory.Exists(root))
        {
            throw new FileNotFoundException($"'{source}' doesn't exist.", source);
        }

        IReadOnlyList<string> excludedDirectories = options.ExcludedDirectories ?? [];
        IReadOnlyList<string> excludedFiles = options.ExcludedFiles ?? [];
        string prefix = "";
        List<SourceItem> items = [];
        if (options.IncludeBaseDirectory)
        {
            prefix = Path.GetFileName(root) + "/";
            items.Add(new SourceItem(root, prefix[..^1], true, 0, Directory.GetLastWriteTimeUtc(root)));
        }

        Walk(root);
        return items;

        void Walk(string directory)
        {
            string[] files = Directory.GetFiles(directory);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                if (!Path.IsInList(file, root, excludedFiles))
                {
                    FileInfo info = new(file);
                    items.Add(new SourceItem(file, prefix + EntryNameFor(file), false, info.Length, info.LastWriteTimeUtc));
                }
            }

            string[] directories = Directory.GetDirectories(directory);
            Array.Sort(directories, StringComparer.Ordinal);
            foreach (string subdirectory in directories)
            {
                DirectoryInfo info = new(subdirectory);
                if (info.LinkTarget is not null || Path.IsInList(subdirectory, root, excludedDirectories))
                {
                    continue;
                }

                items.Add(new SourceItem(subdirectory, prefix + EntryNameFor(subdirectory), true, 0, info.LastWriteTimeUtc));
                Walk(subdirectory);
            }
        }

        string EntryNameFor(string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static async ValueTask WriteItems(List<SourceItem> items, Stream output, ArchiveFormat format, ArchiveCreateOptions options, bool useAsync, CancellationToken cancellationToken)
    {
        Tracker tracker = new(options.Progress, null, null, items.Count, items.Sum(item => item.Length));
        if (format == ArchiveFormat.Zip)
        {
            await WriteZip(items, output, options.CompressionLevel, tracker, useAsync, cancellationToken);
        }
        else
        {
            await WriteTar(items, output, format == ArchiveFormat.TarGz ? options.CompressionLevel : null, tracker, useAsync, cancellationToken);
        }
    }

    private static async ValueTask WriteZip(List<SourceItem> items, Stream output, CompressionLevel level, Tracker tracker, bool useAsync, CancellationToken cancellationToken)
    {
        ZipArchive zip = useAsync
            ? await ZipArchive.CreateAsync(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken)
            : new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        try
        {
            foreach (SourceItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tracker.StartEntry(item.EntryName);
                ZipArchiveEntry entry = item.IsDirectory
                    ? zip.CreateEntry(item.EntryName + "/")
                    : zip.CreateEntry(item.EntryName, level);
                entry.LastWriteTime = ZipTime(item.LastWriteUtc);
                if (TryGetUnixMode(item.FullPath) is { } mode)
                {
                    // The high 16 bits hold st_mode: file type and permissions, as Info-ZIP writes them.
                    entry.ExternalAttributes = ((int)mode | (item.IsDirectory ? 0x4000 : 0x8000)) << 16;
                }

                if (!item.IsDirectory)
                {
                    Stream data = useAsync ? await entry.OpenAsync(cancellationToken) : entry.Open();
                    try
                    {
                        FileStream file = OpenSourceFile(item.FullPath, useAsync);
                        try
                        {
                            await Copy(file, data, tracker, useAsync, cancellationToken);
                        }
                        finally
                        {
                            await DisposeOf(file, useAsync);
                        }
                    }
                    finally
                    {
                        await DisposeOf(data, useAsync);
                    }
                }

                tracker.EndEntry();
            }
        }
        finally
        {
            await DisposeOf(zip, useAsync);
        }
    }

    private static async ValueTask WriteTar(List<SourceItem> items, Stream output, CompressionLevel? gzipLevel, Tracker tracker, bool useAsync, CancellationToken cancellationToken)
    {
        GZipStream? gzip = gzipLevel is { } level ? new GZipStream(output, level, leaveOpen: true) : null;
        try
        {
            TarWriter writer = new(gzip ?? output, TarEntryFormat.Pax, leaveOpen: true);
            try
            {
                foreach (SourceItem item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tracker.StartEntry(item.EntryName);
                    PaxTarEntry entry = new(item.IsDirectory ? TarEntryType.Directory : TarEntryType.RegularFile, item.IsDirectory ? item.EntryName + "/" : item.EntryName)
                    {
                        ModificationTime = new DateTimeOffset(item.LastWriteUtc, TimeSpan.Zero),
                    };
                    if (TryGetUnixMode(item.FullPath) is { } mode)
                    {
                        entry.Mode = mode;
                    }
                    else if (item.IsDirectory)
                    {
                        entry.Mode = DefaultDirectoryMode;
                    }

                    if (item.IsDirectory)
                    {
                        await WriteTarEntry(writer, entry, useAsync, cancellationToken);
                    }
                    else
                    {
                        FileStream file = OpenSourceFile(item.FullPath, useAsync);
                        try
                        {
                            entry.DataStream = new CountingReadStream(file, tracker);
                            await WriteTarEntry(writer, entry, useAsync, cancellationToken);
                        }
                        finally
                        {
                            await DisposeOf(file, useAsync);
                        }
                    }

                    tracker.EndEntry();
                }
            }
            finally
            {
                await DisposeOf(writer, useAsync);
            }
        }
        finally
        {
            if (gzip is not null)
            {
                await DisposeOf(gzip, useAsync);
            }
        }
    }

    private static ValueTask WriteTarEntry(TarWriter writer, TarEntry entry, bool useAsync, CancellationToken cancellationToken)
    {
        if (useAsync)
        {
            return new ValueTask(writer.WriteEntryAsync(entry, cancellationToken));
        }

        writer.WriteEntry(entry);
        return default;
    }

    private static FileStream OpenSourceFile(string path, bool useAsync) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, useAsync);

    /// <summary>Zip stores local times between 1980 and 2107.</summary>
    private static DateTimeOffset ZipTime(DateTime utc)
    {
        DateTime local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        if (local.Year < 1980)
        {
            local = new DateTime(1980, 1, 2, 0, 0, 0, DateTimeKind.Local);
        }
        else if (local.Year > 2107)
        {
            local = new DateTime(2107, 12, 30, 0, 0, 0, DateTimeKind.Local);
        }

        return new DateTimeOffset(local);
    }

    private static UnixFileMode? TryGetUnixMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    // ---- Reading ----

    private readonly record struct ArchiveSource(string? FilePath, Stream? Stream)
    {
        public static ArchiveSource From(string archivePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
            return new ArchiveSource(archivePath, null);
        }

        public static ArchiveSource From(Stream archive)
        {
            ArgumentNullException.ThrowIfNull(archive);
            if (!archive.CanRead)
            {
                throw new ArgumentException("The stream can't be read.", nameof(archive));
            }

            return new ArchiveSource(null, archive);
        }
    }

    private enum EntryKind
    {
        File,
        Directory,
        SymbolicLink,
        HardLink,
        Other,
    }

    /// <summary>An entry as a reader sees it. A tar entry's data can only be read until the reader moves on.</summary>
    private sealed class ReaderEntry(
        string rawName,
        ArchiveEntryInfo info,
        EntryKind kind,
        string? linkName,
        UnixFileMode? mode,
        bool ownsData,
        Func<bool, CancellationToken, ValueTask<Stream>> open)
    {
        public string RawName { get; } = rawName;
        public ArchiveEntryInfo Info { get; } = info;
        public EntryKind Kind { get; } = kind;
        public string? LinkName { get; } = linkName;
        public UnixFileMode? Mode { get; } = mode;

        /// <summary>Whether the stream from <see cref="Open"/> should be disposed. A tar reader still needs its data
        /// stream to skip to the next entry.</summary>
        public bool OwnsData { get; } = ownsData;

        public ValueTask<Stream> Open(bool useAsync, CancellationToken cancellationToken) => open(useAsync, cancellationToken);
    }

    private abstract class EntryReader(Stream? ownedStream) : IDisposable, IAsyncDisposable
    {
        /// <summary>Every entry, when the format has a table of contents (zip); else null.</summary>
        public virtual IReadOnlyList<ReaderEntry>? AllEntries => null;

        public abstract ValueTask<ReaderEntry?> Next(bool useAsync, CancellationToken cancellationToken);

        public void Dispose()
        {
            this.DisposeCore();
            ownedStream?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await this.DisposeCoreAsync();
            if (ownedStream is not null)
            {
                await ownedStream.DisposeAsync();
            }
        }

        protected abstract void DisposeCore();

        protected abstract ValueTask DisposeCoreAsync();
    }

    private sealed class ZipEntryReader : EntryReader
    {
        private readonly ZipArchive _zip;
        private readonly List<ReaderEntry> _entries;
        private int _next;

        public ZipEntryReader(ZipArchive zip, Stream? ownedStream)
            : base(ownedStream)
        {
            this._zip = zip;
            this._entries = zip.Entries.Select(ToReaderEntry).ToList();
        }

        public override IReadOnlyList<ReaderEntry> AllEntries => this._entries;

        public override ValueTask<ReaderEntry?> Next(bool useAsync, CancellationToken cancellationToken) =>
            new(this._next < this._entries.Count ? this._entries[this._next++] : null);

        protected override void DisposeCore() => this._zip.Dispose();

        protected override ValueTask DisposeCoreAsync() => this._zip.DisposeAsync();

        private static ReaderEntry ToReaderEntry(ZipArchiveEntry entry)
        {
            bool isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            int permissions = (entry.ExternalAttributes >> 16) & 0x1FF;
            ArchiveEntryInfo info = new(NormalizeName(entry.FullName), isDirectory ? 0 : entry.Length, entry.LastWriteTime, isDirectory);
            return new ReaderEntry(
                entry.FullName, info, isDirectory ? EntryKind.Directory : EntryKind.File, null,
                permissions != 0 ? (UnixFileMode)permissions : null, ownsData: true,
                async (useAsync, ct) => useAsync ? await entry.OpenAsync(ct) : entry.Open());
        }
    }

    private sealed class TarEntryReader(TarReader reader, Stream? gzip, Stream? ownedStream) : EntryReader(ownedStream)
    {
        public override async ValueTask<ReaderEntry?> Next(bool useAsync, CancellationToken cancellationToken)
        {
            TarEntry? entry = useAsync
                ? await reader.GetNextEntryAsync(copyData: false, cancellationToken)
                : reader.GetNextEntry(copyData: false);
            if (entry is null)
            {
                return null;
            }

            EntryKind kind = entry.EntryType switch
            {
                TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile => EntryKind.File,
                TarEntryType.Directory => EntryKind.Directory,
                TarEntryType.SymbolicLink => EntryKind.SymbolicLink,
                TarEntryType.HardLink => EntryKind.HardLink,
                _ => EntryKind.Other,
            };
            bool isDirectory = kind == EntryKind.Directory;
            ArchiveEntryInfo info = new(NormalizeName(entry.Name), isDirectory ? 0 : entry.Length, entry.ModificationTime, isDirectory);
            Stream data = entry.DataStream ?? Stream.Null;
            return new ReaderEntry(
                entry.Name, info, kind, entry.LinkName, entry.Mode & (UnixFileMode)0x1FF, ownsData: false,
                (_, _) => new ValueTask<Stream>(data));
        }

        protected override void DisposeCore()
        {
            reader.Dispose();
            gzip?.Dispose();
        }

        protected override async ValueTask DisposeCoreAsync()
        {
            await reader.DisposeAsync();
            if (gzip is not null)
            {
                await gzip.DisposeAsync();
            }
        }
    }

    private static async ValueTask<EntryReader> OpenReader(ArchiveSource source, ArchiveFormat? format, bool useAsync, CancellationToken cancellationToken)
    {
        Stream? owned = source.FilePath is null
            ? null
            : new FileStream(source.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync);
        Stream stream = owned ?? source.Stream!;
        try
        {
            ArchiveFormat resolved = format
                ?? (source.FilePath is null ? null : FormatFromPath(source.FilePath))
                ?? await SniffStream(stream, useAsync, cancellationToken);
            switch (resolved)
            {
                case ArchiveFormat.Zip:
                {
                    ZipArchive zip = useAsync
                        ? await ZipArchive.CreateAsync(stream, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken)
                        : new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                    return new ZipEntryReader(zip, owned);
                }
                case ArchiveFormat.Tar:
                    return new TarEntryReader(new TarReader(stream, leaveOpen: true), null, owned);
                default:
                {
                    GZipStream gzip = new(stream, CompressionMode.Decompress, leaveOpen: true);
                    return new TarEntryReader(new TarReader(gzip, leaveOpen: true), gzip, owned);
                }
            }
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }

    private static async ValueTask<ArchiveFormat> SniffStream(Stream stream, bool useAsync, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The archive format can't be detected from a stream that can't seek. Pass the format.", "format");
        }

        long start = stream.Position;
        byte[] header = new byte[262];
        int read = 0;
        while (read < header.Length)
        {
            int count = useAsync
                ? await stream.ReadAsync(header.AsMemory(read), cancellationToken)
                : stream.Read(header, read, header.Length - read);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        stream.Position = start;
        return Sniff(header.AsSpan(0, read)) ?? throw new InvalidDataException("This isn't a zip, tar or tar.gz archive.");
    }

    private static async ValueTask<IReadOnlyList<ArchiveEntryInfo>> GetEntriesCore(ArchiveSource source, ArchiveFormat? format, bool useAsync, CancellationToken cancellationToken)
    {
        EntryReader reader = await OpenReader(source, format, useAsync, cancellationToken);
        try
        {
            List<ArchiveEntryInfo> entries = [];
            while (await reader.Next(useAsync, cancellationToken) is { } entry)
            {
                if (entry.Kind is EntryKind.File or EntryKind.Directory && entry.Info.Name.Length > 0)
                {
                    entries.Add(entry.Info);
                }
            }

            return entries;
        }
        finally
        {
            await DisposeOf(reader, useAsync);
        }
    }

    /// <summary>Finds a file entry. On success the caller owns the reader; on failure it's disposed.</summary>
    private static async ValueTask<(EntryReader Reader, ReaderEntry? Entry)> FindEntry(ArchiveSource source, string entryName, ArchiveFormat? format, bool useAsync, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entryName);
        string wanted = NormalizeName(entryName);
        if (wanted.Length == 0)
        {
            throw new ArgumentException("The entry name is empty.", nameof(entryName));
        }

        EntryReader reader = await OpenReader(source, format, useAsync, cancellationToken);
        try
        {
            while (await reader.Next(useAsync, cancellationToken) is { } entry)
            {
                if (entry.Kind == EntryKind.File && string.Equals(entry.Info.Name, wanted, StringComparison.Ordinal))
                {
                    return (reader, entry);
                }
            }

            return (reader, null);
        }
        catch
        {
            await DisposeOf(reader, useAsync);
            throw;
        }
    }

    private static async ValueTask<bool> ContainsEntryCore(ArchiveSource source, string entryName, ArchiveFormat? format, bool useAsync, CancellationToken cancellationToken)
    {
        (EntryReader reader, ReaderEntry? entry) = await FindEntry(source, entryName, format, useAsync, cancellationToken);
        await DisposeOf(reader, useAsync);
        return entry is not null;
    }

    private static async ValueTask<EntryStream?> OpenEntryCore(ArchiveSource source, string entryName, ArchiveFormat? format, long? maxBytes, bool useAsync, CancellationToken cancellationToken)
    {
        (EntryReader reader, ReaderEntry? entry) = await FindEntry(source, entryName, format, useAsync, cancellationToken);
        if (entry is null)
        {
            await DisposeOf(reader, useAsync);
            return null;
        }

        try
        {
            Stream data = await entry.Open(useAsync, cancellationToken);
            return new EntryStream(data, reader, entry.Info, maxBytes);
        }
        catch
        {
            await DisposeOf(reader, useAsync);
            throw;
        }
    }

    private static async ValueTask ExtractEntryCore(ArchiveSource source, string entryName, string destinationFile, bool overwrite, ArchiveFormat? format, long? maxBytes, bool useAsync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        string target = Path.GetFullPath(destinationFile);
        if (!overwrite && (File.Exists(target) || Directory.Exists(target)))
        {
            throw new IOException($"'{target}' already exists.");
        }

        EntryStream stream = await OpenEntryCore(source, entryName, format, maxBytes, useAsync, cancellationToken)
            ?? throw new FileNotFoundException($"The archive has no file named '{entryName}'.", entryName);
        try
        {
            if (useAsync)
            {
                await AtomicFile.WriteAsync(target, (output, ct) => stream.CopyToAsync(output, ct), cancellationToken);
            }
            else
            {
                AtomicFile.Write(target, stream.CopyTo);
            }
        }
        finally
        {
            await DisposeOf(stream, useAsync);
        }

        TrySetLastWriteTime(target, stream.Info.LastModified, isDirectory: false);
    }

    private static async ValueTask ExtractCore(ArchiveSource source, string destinationDirectory, ArchiveExtractOptions? options, bool useAsync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        options ??= new ArchiveExtractOptions();
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        if (File.Exists(destination))
        {
            throw new IOException($"'{destination}' is a file.");
        }

        // A new destination is filled in a temporary sibling and moved into place, so a failure leaves nothing.
        bool fresh = !Directory.Exists(destination);
        string root = destination;
        if (fresh)
        {
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            root = Path.Combine(parent, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(root);
        }

        try
        {
            EntryReader reader = await OpenReader(source, options.Format, useAsync, cancellationToken);
            try
            {
                await ExtractEntries(reader, root, options, useAsync, cancellationToken);
            }
            finally
            {
                await DisposeOf(reader, useAsync);
            }

            if (fresh)
            {
                Directory.Move(root, destination);
            }
        }
        catch
        {
            if (fresh)
            {
                Directory.DeleteWithRetry(root, maxAttempts: 2, baseDelayMs: 0, throwOnFailure: false);
            }

            throw;
        }
    }

    private static async ValueTask ExtractEntries(EntryReader reader, string root, ArchiveExtractOptions options, bool useAsync, CancellationToken cancellationToken)
    {
        StringComparison comparison = Path.Comparison;

        // With a table of contents (zip), run the filter once up front so progress can show totals.
        HashSet<ReaderEntry>? accepted = null;
        int? entriesTotal = null;
        long? bytesTotal = null;
        if (reader.AllEntries is { } all)
        {
            accepted = all.Where(Accept).ToHashSet();
            entriesTotal = accepted.Count;
            bytesTotal = accepted.Sum(entry => entry.Info.Length);
        }

        Tracker tracker = new(options.Progress, options.MaxEntries, options.MaxTotalBytes, entriesTotal, bytesTotal);
        List<(string Path, DateTimeOffset Time)> directories = [];
        while (await reader.Next(useAsync, cancellationToken) is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(accepted?.Contains(entry) ?? Accept(entry)))
            {
                continue;
            }

            tracker.StartEntry(entry.Info.Name);
            string target = SafeTarget(root, entry, comparison);
            switch (entry.Kind)
            {
                case EntryKind.Directory:
                    Directory.CreateDirectory(target);
                    directories.Add((target, entry.Info.LastModified));
                    break;
                case EntryKind.File:
                    await ExtractFile(entry, target, options.Overwrite, tracker, useAsync, cancellationToken);
                    break;
                case EntryKind.SymbolicLink:
                    CreateSymbolicLink(entry, target, root, options.Overwrite, comparison);
                    break;
                case EntryKind.HardLink:
                    CopyHardLink(entry, target, root, options.Overwrite, tracker, comparison);
                    break;
            }

            tracker.EndEntry();
        }

        // Writing files touches their folders, so folder times go last, deepest first.
        for (int i = directories.Count - 1; i >= 0; i--)
        {
            TrySetLastWriteTime(directories[i].Path, directories[i].Time, isDirectory: true);
        }

        bool Accept(ReaderEntry entry) =>
            entry.Kind != EntryKind.Other
            && entry.Info.Name.Length > 0
            && (options.Filter?.Invoke(entry.Info) ?? true);
    }

    /// <summary>Where an entry goes, or an <see cref="IOException"/> if that's outside <paramref name="root"/>.</summary>
    private static string SafeTarget(string root, ReaderEntry entry, StringComparison comparison)
    {
        string raw = entry.RawName.Replace('\\', '/');
        if (raw.StartsWith('/') || Path.IsPathRooted(entry.RawName) || HasParentSegment(entry.Info.Name))
        {
            throw Outside(entry.RawName);
        }

        string target = Path.GetFullPath(Path.Combine(root, entry.Info.Name.Replace('/', Path.DirectorySeparatorChar)));
        if (target.Length == root.Length || !FileSystemExtensions.IsSameOrInside(target, root, comparison))
        {
            throw Outside(entry.RawName);
        }

        return target;
    }

    private static IOException Outside(string name) =>
        new($"The archive entry '{name}' would be extracted outside the destination folder.");

    private static bool HasParentSegment(string path) =>
        path.Replace('\\', '/').Split('/').Contains("..");

    private static async ValueTask ExtractFile(ReaderEntry entry, string target, bool overwrite, Tracker tracker, bool useAsync, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (overwrite && new FileInfo(target).LinkTarget is not null)
        {
            File.Delete(target);   // write a new file, not through a link to somewhere else
        }

        Stream data = await entry.Open(useAsync, cancellationToken);
        try
        {
            FileStream output = new(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync);
            try
            {
                await Copy(data, output, tracker, useAsync, cancellationToken);
            }
            finally
            {
                await DisposeOf(output, useAsync);
            }
        }
        finally
        {
            if (entry.OwnsData)
            {
                await DisposeOf(data, useAsync);
            }
        }

        TrySetLastWriteTime(target, entry.Info.LastModified, isDirectory: false);
        if (entry.Mode is { } mode && !OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(target, mode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (PlatformNotSupportedException)
            {
            }
        }
    }

    private static void CreateSymbolicLink(ReaderEntry entry, string target, string root, bool overwrite, StringComparison comparison)
    {
        string linkTarget = (entry.LinkName ?? "").Replace('\\', '/');
        if (linkTarget.Length == 0 || linkTarget.StartsWith('/') || Path.IsPathRooted(entry.LinkName))
        {
            throw Outside(entry.RawName);
        }

        string parent = Path.GetDirectoryName(target)!;
        string nativeTarget = linkTarget.Replace('/', Path.DirectorySeparatorChar);
        string resolved = Path.GetFullPath(Path.Combine(parent, nativeTarget));
        // ".." is resolved by the OS after following links, so it's only safe while no folder on the way is a link.
        if (!FileSystemExtensions.IsSameOrInside(resolved, root, comparison) || (HasParentSegment(linkTarget) && HasLinkedAncestor(target, root)))
        {
            throw Outside(entry.RawName);
        }

        Directory.CreateDirectory(parent);
        if (PathExists(target))
        {
            if (!overwrite || Directory.Exists(target) && new DirectoryInfo(target).LinkTarget is null)
            {
                throw new IOException($"'{target}' already exists.");
            }

            File.Delete(target);
        }

        if (Directory.Exists(resolved))
        {
            Directory.CreateSymbolicLink(target, nativeTarget);
        }
        else
        {
            File.CreateSymbolicLink(target, nativeTarget);
        }
    }

    private static void CopyHardLink(ReaderEntry entry, string target, string root, bool overwrite, Tracker tracker, StringComparison comparison)
    {
        string linkName = NormalizeName(entry.LinkName ?? "");
        string source = Path.GetFullPath(Path.Combine(root, linkName.Replace('/', Path.DirectorySeparatorChar)));
        if (linkName.Length == 0 || HasParentSegment(linkName) || !FileSystemExtensions.IsSameOrInside(source, root, comparison))
        {
            throw Outside(entry.RawName);
        }

        if (!File.Exists(source))
        {
            throw new InvalidDataException($"The archive entry '{entry.RawName}' links to '{entry.LinkName}', which hasn't been extracted.");
        }

        tracker.AddBytes(new FileInfo(source).Length);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (overwrite && new FileInfo(target).LinkTarget is not null)
        {
            File.Delete(target);
        }

        File.Copy(source, target, overwrite);
    }

    private static bool HasLinkedAncestor(string path, string root)
    {
        for (string? directory = Path.GetDirectoryName(path); directory is not null && directory.Length > root.Length; directory = Path.GetDirectoryName(directory))
        {
            if (new DirectoryInfo(directory).LinkTarget is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathExists(string path) =>
        File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    private static void TrySetLastWriteTime(string path, DateTimeOffset time, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.SetLastWriteTimeUtc(path, time.UtcDateTime);
            }
            else
            {
                File.SetLastWriteTimeUtc(path, time.UtcDateTime);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // A time this file system can't store; the file is still extracted.
        }
    }

    // ---- Shared plumbing ----

    private static async ValueTask Copy(Stream from, Stream to, Tracker tracker, bool useAsync, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = useAsync
                    ? await from.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    : from.Read(buffer, 0, BufferSize);
                if (read == 0)
                {
                    return;
                }

                tracker.AddBytes(read);   // before writing, so a limit stops the write
                if (useAsync)
                {
                    await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                else
                {
                    to.Write(buffer, 0, read);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ValueTask DisposeOf<T>(T disposable, bool useAsync)
        where T : IDisposable, IAsyncDisposable
    {
        if (useAsync)
        {
            return disposable.DisposeAsync();
        }

        disposable.Dispose();
        return default;
    }

    /// <summary>Counts entries and bytes, enforces limits, and reports progress.</summary>
    private sealed class Tracker(IProgress<ArchiveProgress>? progress, int? maxEntries, long? maxBytes, int? entriesTotal, long? bytesTotal)
    {
        private int _entries;
        private long _bytes;
        private long _reportedBytes;
        private string? _current;

        public void StartEntry(string name)
        {
            if (maxEntries is { } max && this._entries >= max)
            {
                throw new InvalidDataException($"The archive has more than {max} entries.");
            }

            this._current = name;
        }

        public void AddBytes(long count)
        {
            this._bytes += count;
            if (maxBytes is { } max && this._bytes > max)
            {
                throw new InvalidDataException($"The archive holds more than {max} bytes.");
            }

            if (this._bytes - this._reportedBytes >= ProgressStep)
            {
                this.Report();
            }
        }

        public void EndEntry()
        {
            this._entries++;
            this.Report();
        }

        private void Report()
        {
            this._reportedBytes = this._bytes;
            progress?.Report(new ArchiveProgress(this._entries, entriesTotal, this._bytes, bytesTotal, this._current));
        }
    }

    private abstract class ReadOnlyWrapperStream(Stream inner) : Stream
    {
        protected Stream Inner { get; } = inner;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Counted(this.Inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => this.Counted(this.Inner.Read(buffer));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.Counted(await this.Inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.Counted(await this.Inner.ReadAsync(buffer, cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected abstract void OnRead(int count);

        private int Counted(int count)
        {
            this.OnRead(count);
            return count;
        }
    }

    /// <summary>A file's data for <see cref="TarWriter"/>, which needs its length, reporting bytes as they're read.</summary>
    private sealed class CountingReadStream(Stream inner, Tracker tracker) : ReadOnlyWrapperStream(inner)
    {
        public override bool CanSeek => this.Inner.CanSeek;

        public override long Length => this.Inner.Length;

        public override long Position
        {
            get => this.Inner.Position;
            set => this.Inner.Position = value;
        }

        public override long Seek(long offset, SeekOrigin origin) => this.Inner.Seek(offset, origin);

        protected override void OnRead(int count) => tracker.AddBytes(count);
    }

    /// <summary>An entry's data from <see cref="OpenEntry(string, string, ArchiveFormat?, long?)"/>. Enforces the
    /// byte limit, and closes the archive when disposed.</summary>
    private sealed class EntryStream(Stream inner, EntryReader reader, ArchiveEntryInfo info, long? maxBytes) : ReadOnlyWrapperStream(inner)
    {
        private long _read;
        private bool _disposed;

        public ArchiveEntryInfo Info { get; } = info;

        public override async ValueTask DisposeAsync()
        {
            if (!this._disposed)
            {
                this._disposed = true;
                await this.Inner.DisposeAsync();
                await reader.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }

        protected override void OnRead(int count)
        {
            this._read += count;
            if (maxBytes is { } max && this._read > max)
            {
                throw new InvalidDataException($"The archive entry '{this.Info.Name}' is bigger than {max} bytes.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this._disposed)
            {
                this._disposed = true;
                this.Inner.Dispose();
                reader.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
