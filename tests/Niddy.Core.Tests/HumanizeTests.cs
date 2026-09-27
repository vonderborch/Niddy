using System.Globalization;
using Niddy.Helpers;

namespace Niddy.Core.Tests;

public class HumanizeTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1_500, "1.5 KB")]
    [InlineData(2_000_000, "2 MB")]
    [InlineData(3_210_000_000, "3.2 GB")]
    public void Bytes_UsesDecimalUnits(long bytes, string expected) =>
        Assert.Equal(expected, Humanize.Bytes(bytes, provider: Invariant));

    [Fact]
    public void Bytes_Binary_UsesKibibytes() =>
        Assert.Equal("1.5 KiB", Humanize.Bytes(1536, binary: true, provider: Invariant));

    [Theory]
    [InlineData(950, "950")]
    [InlineData(1_200, "1.2K")]
    [InlineData(999_960, "1M")]
    [InlineData(4_500_000_000, "4.5B")]
    public void Count_Abbreviates(long count, string expected) =>
        Assert.Equal(expected, Humanize.Count(count, provider: Invariant));

    [Fact]
    public void Duration_FormatsByMagnitude()
    {
        Assert.Equal("450 ms", Humanize.Duration(TimeSpan.FromMilliseconds(450), provider: Invariant));
        Assert.Equal("3.2 s", Humanize.Duration(TimeSpan.FromMilliseconds(3_290), provider: Invariant));
        Assert.Equal("1 h 5 min", Humanize.Duration(new TimeSpan(1, 5, 30), provider: Invariant));
        Assert.Equal("2 d", Humanize.Duration(new TimeSpan(2, 0, 7, 0), provider: Invariant));
        Assert.Equal("-2 min", Humanize.Duration(TimeSpan.FromMinutes(-2), provider: Invariant));
    }

    [Fact]
    public void RelativeTime_DescribesPastAndFuture()
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("just now", Humanize.RelativeTime(now.AddSeconds(-10), now));
        Assert.Equal("5 minutes ago", Humanize.RelativeTime(now.AddMinutes(-5), now));
        Assert.Equal("in 3 hours", Humanize.RelativeTime(now.AddHours(3), now));
        Assert.Equal("yesterday", Humanize.RelativeTime(now.AddHours(-30), now));
        Assert.Equal("tomorrow", Humanize.RelativeTime(now.AddHours(30), now));
        Assert.Equal("4 days ago", Humanize.RelativeTime(now.AddDays(-4), now));
        Assert.Equal("2 months ago", Humanize.RelativeTime(now.AddDays(-62), now));
        Assert.Equal("in 1 year", Humanize.RelativeTime(now.AddDays(400), now));
    }

    [Fact]
    public void Plural_And_Ordinal()
    {
        Assert.Equal("1 file", Humanize.Plural(1, "file", provider: Invariant));
        Assert.Equal("3 files", Humanize.Plural(3, "file", provider: Invariant));
        Assert.Equal("2 children", Humanize.Plural(2, "child", "children", Invariant));
        Assert.Equal("1st", Humanize.Ordinal(1));
        Assert.Equal("2nd", Humanize.Ordinal(2));
        Assert.Equal("11th", Humanize.Ordinal(11));
        Assert.Equal("23rd", Humanize.Ordinal(23));
        Assert.Equal("112th", Humanize.Ordinal(112));
    }
}
