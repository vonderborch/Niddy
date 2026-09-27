using Niddy.Helpers;

namespace Niddy.Core.Tests;

public class ListExtensionsTests
{
    [Fact]
    public void CombineLists_KeepsOrderAndLeavesInputsAlone()
    {
        var first = new List<int> { 1, 2 };
        var second = new List<int> { 3 };

        Assert.Equal([1, 2, 3], first.CombineLists(second));
        Assert.Equal(2, first.Count);
    }

    [Fact]
    public void CompareLists_IgnoresOrder()
    {
        Assert.True(new List<int> { 3, 1, 2 }.CompareLists([1, 2, 3]));
        Assert.False(new List<int> { 1, 2 }.CompareLists([1, 2, 3]));
        Assert.False(new List<int> { 1, 1, 2 }.CompareLists([1, 2, 2]));
    }

    [Fact]
    public void IsContained_StringsHonourCaseSensitivity()
    {
        var list = new List<string> { "Alpha", "Beta" };

        Assert.False(list.IsContained("alpha"));
        Assert.True(list.IsContained("alpha", caseSensitive: false));
        Assert.False(new List<string>().IsContained("alpha"));
    }
}
