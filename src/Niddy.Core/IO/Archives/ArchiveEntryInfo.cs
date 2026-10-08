namespace Niddy.Core.IO.Archives;

/// <summary>An entry in an archive, as listed by <see cref="Archive.GetEntries(string, ArchiveFormat?)"/>.</summary>
/// <param name="Name">
/// The entry's path inside the archive, with <c>/</c> separators and no leading <c>./</c>, leading <c>/</c> or
/// trailing <c>/</c>, e.g. <c>docs/readme.md</c>.
/// </param>
/// <param name="Length">The uncompressed size in bytes, as the archive states it. 0 for a directory.</param>
/// <param name="LastModified">When the entry was last modified.</param>
/// <param name="IsDirectory">Whether the entry is a directory.</param>
public sealed record ArchiveEntryInfo(string Name, long Length, DateTimeOffset LastModified, bool IsDirectory);
