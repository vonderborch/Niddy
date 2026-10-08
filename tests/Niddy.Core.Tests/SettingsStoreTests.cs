using Niddy.Core.Settings;

namespace Niddy.Core.Tests;

// Watch creates a DebouncedFileWatcher, which reads Platform.Info; PlatformTests briefly make that throw.
[Collection(PlatformStateCollection.Name)]
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
        TestStore store = new(FilePath);

        Assert.Equal(7, store.Current.Volume);
        Assert.Equal(2, store.Current.Version);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Update_SavesAndRaisesChanged()
    {
        TestStore store = new(FilePath);
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
        TestStore store = new(FilePath);
        store.Update(s => s.Volume = 1);

        store.Reset();

        Assert.Equal(7, store.Current.Volume);
        Assert.Equal(7, new TestStore(FilePath).Current.Volume);
    }

    [Fact]
    public void CorruptFile_IsSetAsideAndDefaultsUsed()
    {
        File.WriteAllText(FilePath, "{ not json");
        TestStore store = new(FilePath);
        SettingsCorruptFileEventArgs? corruptArgs = null;
        store.CorruptFileDetected += (_, e) => corruptArgs = e;

        Assert.Equal(7, store.Current.Volume);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.False(File.Exists(FilePath));
        Assert.NotNull(corruptArgs);
        Assert.Equal(Path.GetFullPath(FilePath), corruptArgs.FilePath);
        Assert.True(corruptArgs.IsSetAside);
        Assert.NotNull(corruptArgs.Exception);
    }

    [Fact]
    public void CorruptFile_WhenOverrideDisabled_KeepsFileAndRaisesEvent()
    {
        File.WriteAllText(FilePath, "{ not json");
        TestStore store = new(FilePath) { OverrideCorruptFile = false };
        SettingsCorruptFileEventArgs? corruptArgs = null;
        store.CorruptFileDetected += (_, e) => corruptArgs = e;

        Assert.Equal(7, store.Current.Volume);
        Assert.False(File.Exists(FilePath + ".bad"));
        Assert.True(File.Exists(FilePath));
        Assert.Equal("{ not json", File.ReadAllText(FilePath));
        Assert.NotNull(corruptArgs);
        Assert.Equal(Path.GetFullPath(FilePath), corruptArgs.FilePath);
        Assert.False(corruptArgs.IsSetAside);
        Assert.NotNull(corruptArgs.Exception);
    }

    [Fact]
    public void ReloadIfChangedExternally_IgnoresOwnWritesAndPicksUpOthers()
    {
        TestStore store = new(FilePath);
        int loads = 0;
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
        using TestStore store = new(FilePath);
        store.Update(s => s.Volume = 3);
        TaskCompletionSource reloaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += (_, e) =>
        {
            if (e.Source == SettingsChangeSource.Load)
            {
                reloaded.TrySetResult();
            }
        };
        store.Watch(TimeSpan.FromMilliseconds(100));

        await File.WriteAllTextAsync(FilePath, """{ "volume": 11 }""", TestContext.Current.CancellationToken);
        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(11, store.Current.Volume);
    }
}
