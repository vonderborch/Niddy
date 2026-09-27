using Niddy.Settings;

namespace Niddy.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-settings-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string FilePath => Path.Combine(_dir, "settings.json");

    public sealed class TestSettings
    {
        public string Theme { get; set; } = "Light";
        public int Volume { get; set; } = 5;
        public int Version { get; set; }
    }

    private sealed class TestStore(string path) : SettingsStore<TestSettings>(path)
    {
        protected override TestSettings CreateDefault() => new() { Volume = 7 };

        protected override void OnLoaded(TestSettings settings) => settings.Version = 2;
    }

    [Fact]
    public void Current_WithoutAFile_UsesDefaults()
    {
        var store = new TestStore(FilePath);

        Assert.Equal(7, store.Current.Volume);
        Assert.Equal(2, store.Current.Version);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Update_SavesAndRaisesChanged()
    {
        var store = new TestStore(FilePath);
        SettingsChangedEventArgs<TestSettings>? raised = null;
        store.Changed += (_, e) => raised = e;

        store.Update(s => s.Theme = "Dark");

        Assert.Equal(SettingsChangeSource.Update, raised?.Source);
        Assert.Equal("Dark", new TestStore(FilePath).Current.Theme);
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var store = new TestStore(FilePath);
        store.Update(s => s.Volume = 1);

        store.Reset();

        Assert.Equal(7, store.Current.Volume);
        Assert.Equal(7, new TestStore(FilePath).Current.Volume);
    }

    [Fact]
    public void CorruptFile_IsSetAsideAndDefaultsUsed()
    {
        File.WriteAllText(FilePath, "{ not json");
        var store = new TestStore(FilePath);

        Assert.Equal(7, store.Current.Volume);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void ReloadIfChangedExternally_IgnoresOwnWritesAndPicksUpOthers()
    {
        var store = new TestStore(FilePath);
        var loads = 0;
        store.Changed += (_, e) => loads += e.Source == SettingsChangeSource.Load ? 1 : 0;
        store.Update(s => s.Volume = 3);

        store.ReloadIfChangedExternally();
        Assert.Equal(0, loads);

        File.WriteAllText(FilePath, """{ "theme": "Blue", "volume": 9 }""");
        store.ReloadIfChangedExternally();

        Assert.Equal(1, loads);
        Assert.Equal("Blue", store.Current.Theme);
        Assert.Equal(9, store.Current.Volume);
    }

    [Fact]
    public async Task Watch_ReloadsWhenTheFileChanges()
    {
        using var store = new TestStore(FilePath);
        store.Update(s => s.Volume = 3);
        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += (_, e) =>
        {
            if (e.Source == SettingsChangeSource.Load)
                reloaded.TrySetResult();
        };
        store.Watch(TimeSpan.FromMilliseconds(100));

        await File.WriteAllTextAsync(FilePath, """{ "volume": 11 }""", TestContext.Current.CancellationToken);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(11, store.Current.Volume);
    }
}
