using Niddy.Helpers;

namespace Niddy.Core.Tests;

public class ByteHelpersTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024L * 1024 * 1024 * 1024 * 1024, "1024.0 TB")]
    public void FormatBytes(long bytes, string expected)
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, ByteHelpers.FormatBytes(bytes));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }
}
