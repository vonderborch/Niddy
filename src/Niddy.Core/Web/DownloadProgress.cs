namespace Niddy.Core.Web;

/// <summary>How far a <see cref="WebContent.DownloadFileAsync"/> download has got.</summary>
/// <param name="BytesReceived">The bytes written to the file so far.</param>
/// <param name="TotalBytes">The download's size from its <c>Content-Length</c> header, or null if the server didn't say.</param>
public readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes)
{
    /// <summary>
    /// <see cref="BytesReceived"/> as a fraction of <see cref="TotalBytes"/>, from 0 to 1, or null when the size isn't
    /// known. Bind it to a progress bar, or show an indeterminate one while it's null.
    /// </summary>
    public double? Fraction => this.TotalBytes switch
    {
        null => null,
        <= 0 => 1,
        long total => Math.Min(1, (double)this.BytesReceived / total),
    };
}
