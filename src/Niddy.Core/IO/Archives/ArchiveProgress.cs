namespace Niddy.Core.IO.Archives;

/// <summary>How far an archive operation has got, reported to <see cref="ArchiveCreateOptions.Progress"/> or
/// <see cref="ArchiveExtractOptions.Progress"/>.</summary>
/// <param name="EntriesDone">Entries (files and directories) finished so far.</param>
/// <param name="EntriesTotal">Entries in all, or null if that isn't known without an extra pass (extracting a tar).</param>
/// <param name="BytesDone">Uncompressed bytes written so far.</param>
/// <param name="BytesTotal">Uncompressed bytes in all, or null if that isn't known (extracting a tar).</param>
/// <param name="CurrentEntry">The entry being worked on, or that just finished.</param>
public readonly record struct ArchiveProgress(int EntriesDone, int? EntriesTotal, long BytesDone, long? BytesTotal, string? CurrentEntry);
