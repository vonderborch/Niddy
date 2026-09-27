using Niddy.Helpers;

namespace Niddy.Core.Tests;

public class StringExtensionsTests
{
    [Fact]
    public void Join_FormatsNonStringsAndNulls()
    {
        Assert.Equal("1, 2, 3", ", ".Join(new[] { 1, 2, 3 }));
        Assert.Equal("a--b", new[] { "a", null, "b" }.Join("-"));
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("  ", false, true)]
    [InlineData("x", false, false)]
    public void NullOrEmptyChecks(string? value, bool empty, bool whiteSpace)
    {
        Assert.Equal(empty, value.IsNullOrEmpty());
        Assert.Equal(whiteSpace, value.IsNullOrWhiteSpace());
    }
}
