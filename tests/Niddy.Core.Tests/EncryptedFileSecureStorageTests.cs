using Niddy.Security;

namespace Niddy.Core.Tests;

public sealed class EncryptedFileSecureStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "niddy-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SetToken_RoundTripsAndNullRemoves()
    {
        var storage = new EncryptedFileSecureStorage(_directory, "tests");

        Assert.False(storage.TokenExists("key"));
        storage.SetToken("key", "secret value");
        Assert.True(storage.TokenExists("key"));
        Assert.Equal("secret value", storage.GetToken("key"));

        storage.SetToken("key", null);
        Assert.False(storage.TokenExists("key"));
        Assert.Null(storage.GetToken("key"));
    }

    [Fact]
    public void StoredFilesDoNotContainThePlainText()
    {
        var storage = new EncryptedFileSecureStorage(_directory, "tests");
        storage.SetToken("key", "plain-text-marker");

        foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain("plain-text-marker", File.ReadAllText(file));
    }

    [Fact]
    public void AnotherInstanceReadsTheSameTokens()
    {
        new EncryptedFileSecureStorage(_directory, "tests").SetToken("key", "value");

        Assert.Equal("value", new EncryptedFileSecureStorage(_directory, "tests").GetToken("key"));
    }
}
