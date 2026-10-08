namespace Niddy.Core.IO.Archives;

/// <summary>An archive format <see cref="Archive"/> can read and write.</summary>
public enum ArchiveFormat
{
    /// <summary>A zip file (<c>.zip</c>). Entries can be read in any order, so reading one entry is cheap.</summary>
    Zip,

    /// <summary>An uncompressed tar file (<c>.tar</c>). Entries are read in order.</summary>
    Tar,

    /// <summary>A gzip-compressed tar file (<c>.tar.gz</c> or <c>.tgz</c>). Entries are read in order.</summary>
    TarGz,
}
