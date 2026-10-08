using System.Text;
using Niddy.Core.IO;
using Niddy.Core.Text;

namespace Niddy.Core.Tests;

public class ExtensionsTests
{
    private sealed class Disposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => this.Disposed = true;
    }

    [Fact]
    public void DisposeIfPossible_DisposesOnlyDisposables()
    {
        Disposable disposable = new();
        object boxed = new Disposable();
        string? nothing = null;

        disposable.DisposeIfPossible();
        boxed.DisposeIfPossible();
        nothing.DisposeIfPossible();
        42.DisposeIfPossible();

        Assert.True(disposable.Disposed);
        Assert.True(((Disposable)boxed).Disposed);
    }

    [Fact]
    public void Stream_ReadsTheRest()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("héllo"));
        stream.Position = 0;

        Assert.Equal("héllo", stream.ReadToString());
        Assert.Empty(stream.ReadToBytes());
    }

    [Fact]
    public async Task Stream_ReadsTheRestAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryStream stream = new(Encoding.Latin1.GetBytes("abc"));

        Assert.Equal("abc", await stream.ReadToStringAsync(Encoding.Latin1, ct));

        stream.Position = 1;
        Assert.Equal("bc"u8.ToArray(), await stream.ReadToBytesAsync(ct));
    }

    [Fact]
    public void IsNullOrEmpty_TellsTheCompilerTheStringIsNotNull()
    {
        string? value = "abc";

        // Compiles without a nullable warning only because of [NotNullWhen(false)] on the receiver.
        int length = value.IsNullOrEmpty() ? 0 : value.Length;
        int trimmed = value.IsNullOrWhiteSpace() ? 0 : value.Trim().Length;

        Assert.Equal(3, length);
        Assert.Equal(3, trimmed);
    }
}
