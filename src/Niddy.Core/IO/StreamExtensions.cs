using System.Text;

namespace Niddy.Core.IO;

/// <summary>
/// Extensions on <see cref="Stream"/> for reading the rest of a stream in one call:
/// <c>byte[] data = stream.ReadToBytes();</c>.
/// </summary>
public static class StreamExtensions
{
    extension(Stream stream)
    {
        /// <summary>
        /// Reads the rest of the stream and returns it as a byte array.
        /// </summary>
        public byte[] ReadToBytes()
        {
            using MemoryStream memoryStream = new();
            stream.CopyTo(memoryStream);
            byte[] output = memoryStream.ToArray();
            return output;
        }

        /// <summary>
        /// Asynchronously reads the rest of the stream and returns it as a byte array.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read.</param>
        public async Task<byte[]> ReadToBytesAsync(CancellationToken cancellationToken = default)
        {
            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            byte[] output = memoryStream.ToArray();
            return output;
        }

        /// <summary>
        /// Reads the rest of the stream and returns it as a string.
        /// </summary>
        /// <param name="encoding">The text's encoding. Null is UTF-8.</param>
        public string ReadToString(Encoding? encoding = null)
        {
            string output = (encoding ?? Encoding.UTF8).GetString(stream.ReadToBytes());
            return output;
        }

        /// <summary>
        /// Asynchronously reads the rest of the stream and returns it as a string.
        /// </summary>
        /// <param name="encoding">The text's encoding. Null is UTF-8.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public async Task<string> ReadToStringAsync(Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            byte[] bytes = await stream.ReadToBytesAsync(cancellationToken);
            string output = (encoding ?? Encoding.UTF8).GetString(bytes);
            return output;
        }
    }
}
