using Niddy.Core.Security;
using Niddy.Core.Security.Platforms;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.Tests;

// SecureStorageFactory's providers are static, and Create reads Platform.Info, so this class shares
// PlatformStateCollection with PlatformTests. Every registration is disposed.
[Collection(PlatformStateCollection.Name)]
public sealed class SecureStorageFactoryTests
{
    private sealed class FakeStorage(string name) : ISecureStorage
    {
        public string Name { get; } = name;

        public string? GetToken(string key)
        {
            return null;
        }

        public void SetToken(string key, string? value)
        {
        }

        public bool TokenExists(string key)
        {
            return false;
        }
    }

    [Fact]
    public void Create_UsesRegisteredProvider_WithContext()
    {
        SecureStorageContext? seen = null;
        using IDisposable registration = SecureStorageFactory.Register(context =>
        {
            seen = context;
            return new FakeStorage("custom");
        });

        ISecureStorage storage = SecureStorageFactory.Create("/tmp/base", "MyApp");

        Assert.Equal("custom", Assert.IsType<FakeStorage>(storage).Name);
        Assert.Equal(new SecureStorageContext("/tmp/base", "MyApp", Platform.Info.OS), seen);
    }

    [Fact]
    public void Create_UndeclaredOS_Throws()
    {
        OS other = Platform.Info.OS == OS.Wasi ? OS.Browser : OS.Wasi;
        try
        {
            Assert.Throws<UnsupportedOperatingSystemException>(() => Platform.DeclareSupportedOperatingSystems(other));
            Assert.Throws<UnsupportedOperatingSystemException>(() => SecureStorageFactory.Create(Path.GetTempPath(), "tests"));
        }
        finally
        {
            Platform.ClearSupportedOperatingSystems();
        }
    }

    [Theory]
    [InlineData(OS.Android)]
    [InlineData(OS.FreeBSD)]
    [InlineData(OS.Wasi)]
    [InlineData(OS.Unknown)]
    public void CreateDefault_NoNativeStore_UsesEncryptedFiles(OS os)
    {
        string directory = Path.Combine(Path.GetTempPath(), "niddy-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.IsType<EncryptedFileSecureStorage>(SecureStorageFactory.CreateDefault(new SecureStorageContext(directory, "tests", os)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Create_TriesNewestFirst_AndNullPasses()
    {
        using IDisposable older = SecureStorageFactory.Register(_ => new FakeStorage("older"));
        using IDisposable newer = SecureStorageFactory.Register(_ => null);

        Assert.Equal("older", Assert.IsType<FakeStorage>(SecureStorageFactory.Create("base")).Name);

        using IDisposable newest = SecureStorageFactory.Register(_ => new FakeStorage("newest"));

        Assert.Equal("newest", Assert.IsType<FakeStorage>(SecureStorageFactory.Create("base")).Name);
    }

    [Fact]
    public void Create_AllProvidersPass_UsesDefault()
    {
        using IDisposable registration = SecureStorageFactory.Register(_ => null);

        Assert.IsNotType<FakeStorage>(SecureStorageFactory.Create(Path.GetTempPath(), "tests"));
    }

    [Fact]
    public void DisposingRegistration_RemovesOnlyThatProvider()
    {
        using IDisposable first = SecureStorageFactory.Register(_ => new FakeStorage("first"));
        IDisposable second = SecureStorageFactory.Register(_ => new FakeStorage("second"));

        second.Dispose();
        second.Dispose();

        Assert.Equal("first", Assert.IsType<FakeStorage>(SecureStorageFactory.Create("base")).Name);
    }

    [Fact]
    public void Provider_CanWrapDefault()
    {
        using IDisposable registration = SecureStorageFactory.Register(context =>
            new FakeStorage(SecureStorageFactory.CreateDefault(context).GetType().Name));

        FakeStorage storage = Assert.IsType<FakeStorage>(SecureStorageFactory.Create(Path.GetTempPath(), "tests"));

        Assert.NotEqual(nameof(FakeStorage), storage.Name);
    }

    [Theory]
    [InlineData(OS.MacOS)]
    [InlineData(OS.iOS)]
    [InlineData(OS.iPadOS)]
    [InlineData(OS.TvOS)]
    [InlineData(OS.WatchOS)]
    [InlineData(OS.MacCatalyst)]
    public void CreateDefault_ApplePlatforms_UseKeychain(OS os)
    {
        // Only constructs it; nothing touches the real keychain.
        Assert.IsType<AppleKeychainStorage>(SecureStorageFactory.CreateDefault(new SecureStorageContext("base", "tests", os)));
    }

    [Fact]
    public void CreateDefault_Windows_UsesNativeStore()
    {
        Assert.IsNotType<EncryptedFileSecureStorage>(SecureStorageFactory.CreateDefault(new SecureStorageContext("base", "tests", OS.Windows)));
    }

    [Fact]
    public void CreateDefault_Browser_Throws()
    {
        PlatformNotSupportedException ex = Assert.Throws<PlatformNotSupportedException>(
            () => SecureStorageFactory.CreateDefault(new SecureStorageContext("base", "tests", OS.Browser)));
        Assert.Contains(nameof(SecureStorageFactory.Register), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_Browser_ProviderStillWins()
    {
        using IDisposable registration = SecureStorageFactory.Register(_ => new FakeStorage("browser"));

        Assert.Equal("browser", Assert.IsType<FakeStorage>(SecureStorageFactory.Create("base")).Name);
    }

    [Fact]
    public void AppleKeychain_RoundTripsOnMacOS()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Needs the macOS keychain.");

        // A unique service so nothing real is touched; every item is removed at the end.
        AppleKeychainStorage storage = new("niddy-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(storage.TokenExists("key"));
            Assert.Null(storage.GetToken("key"));

            storage.SetToken("key", "first ✓");
            Assert.True(storage.TokenExists("key"));
            Assert.Equal("first ✓", storage.GetToken("key"));

            storage.SetToken("key", "second");
            Assert.Equal("second", storage.GetToken("key"));

            storage.SetToken("empty", "");
            Assert.Equal("", storage.GetToken("empty"));

            storage.SetToken("key", null);
            Assert.False(storage.TokenExists("key"));
            Assert.Null(storage.GetToken("key"));
            storage.SetToken("key", null);
        }
        finally
        {
            storage.SetToken("key", null);
            storage.SetToken("empty", null);
        }
    }

    [Fact]
    public void Register_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecureStorageFactory.Register(null!));
    }
}
