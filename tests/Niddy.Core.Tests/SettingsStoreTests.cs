using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.Serialization;
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

    public sealed class VersionedSettings
    {
        public string Color { get; set; } = "Light";
        public int Volume { get; set; } = 5;
        public int Loudness { get; set; }
    }

    // v0 (before versioning) had "colour"; v1 renamed it to "color"; v2 made volume out of 100 instead of 10.
    private sealed class VersionedStore(string path) : SettingsStore<VersionedSettings>(path)
    {
        public List<int> Steps { get; } = [];

        public int? UpgradedFrom { get; private set; }

        public int ThrowOnStep { get; init; } = -1;

        public bool ThrowOnUpgraded { get; init; }

        public List<SettingsMigrationException> Failures { get; } = [];

        protected override SettingsMigrationFailureAction OnMigrationFailed(SettingsMigrationException exception)
        {
            Failures.Add(exception);
            return base.OnMigrationFailed(exception);
        }

        public override int Version => 2;

        protected override void ConfigureMigrations(SettingsMigrations migrations) => migrations
            .Add(fromVersion: 0, json => Step(json, 0, () => json.RenameProperty("colour", "color")))
            .Add(fromVersion: 1, json => Step(json, 1, () => json["volume"] = json["volume"]!.GetValue<int>() * 10));

        private void Step(JsonObject json, int fromVersion, Action change)
        {
            Steps.Add(fromVersion);
            if (fromVersion == ThrowOnStep)
            {
                json["color"] = "Half-done";   // a partial change, undone by SkipStep
                throw new InvalidOperationException("boom");
            }

            change();
        }

        protected override void OnUpgraded(VersionedSettings settings, int fromVersion)
        {
            UpgradedFrom = fromVersion;
            settings.Loudness = settings.Volume / 10;
            if (ThrowOnUpgraded)
            {
                settings.Color = "Half-done";
                throw new InvalidOperationException("boom");
            }
        }
    }

    private string[] Backups() => Directory.GetFiles(_dir, "settings.json.v*.bak");

    [Fact]
    public void Versioned_WritesTheVersionFirst()
    {
        VersionedStore store = new(FilePath);

        store.Update(s => s.Color = "Dark");

        string text = File.ReadAllText(FilePath).ReplaceLineEndings("\n");
        Assert.StartsWith("{\n  \"$version\": 2,\n  \"color\": \"Dark\"", text);
        Assert.Null(store.FileVersion);
        Assert.Empty(store.Steps);
    }

    [Fact]
    public void Unversioned_WritesNoVersion()
    {
        TestStore store = new(FilePath);

        store.Update(s => s.Volume = 1);

        Assert.DoesNotContain("$version", File.ReadAllText(FilePath));
        Assert.Null(store.FileVersion);
    }

    [Fact]
    public void Versioned_UpgradesAFileWithoutAVersionStepByStep()
    {
        const string old = """{ "colour": "Blue", "volume": 3 }""";
        File.WriteAllText(FilePath, old);
        VersionedStore store = new(FilePath);

        VersionedSettings settings = store.Current;

        Assert.Equal([0, 1], store.Steps);
        Assert.Equal("Blue", settings.Color);
        Assert.Equal(30, settings.Volume);
        Assert.Equal(0, store.UpgradedFrom);
        Assert.Equal(3, settings.Loudness);
        Assert.Equal(0, store.FileVersion);

        // Saved straight away at the new version, with the old file backed up.
        VersionedStore reread = new(FilePath);
        Assert.Equal(30, reread.Current.Volume);
        Assert.Empty(reread.Steps);
        Assert.Equal(2, reread.FileVersion);
        string backup = Assert.Single(Backups());
        Assert.Contains(".v0-", Path.GetFileName(backup));
        Assert.Equal(old, File.ReadAllText(backup));
    }

    [Fact]
    public void Versioned_RunsOnlyTheStepsAfterTheFilesVersion()
    {
        File.WriteAllText(FilePath, """{ "$version": 1, "color": "Blue", "volume": 4 }""");
        VersionedStore store = new(FilePath);

        Assert.Equal(40, store.Current.Volume);
        Assert.Equal([1], store.Steps);
        Assert.Equal(1, store.UpgradedFrom);
        Assert.Equal(1, store.FileVersion);
    }

    [Fact]
    public void Versioned_CurrentFileIsNotMigratedOrRewritten()
    {
        const string text = """{ "$version": 2, "color": "Blue", "volume": 40 }""";
        File.WriteAllText(FilePath, text);
        VersionedStore store = new(FilePath);

        Assert.Equal(40, store.Current.Volume);
        Assert.Empty(store.Steps);
        Assert.Null(store.UpgradedFrom);
        Assert.Equal(2, store.FileVersion);
        Assert.Equal(text, File.ReadAllText(FilePath));
        Assert.Empty(Backups());
    }

    [Fact]
    public void Versioned_NewerFileIsReadAsIsAndKeepsItsVersion()
    {
        File.WriteAllText(FilePath, """{ "$version": 5, "color": "Blue", "volume": 40, "future": true }""");
        VersionedStore store = new(FilePath);

        Assert.Equal("Blue", store.Current.Color);
        Assert.Empty(store.Steps);
        Assert.Equal(5, store.FileVersion);

        store.Update(s => s.Volume = 50);

        Assert.Contains("\"$version\": 5", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Versioned_BackupCanBeTurnedOff()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath) { BackupBeforeUpgrade = false };

        Assert.Equal(30, store.Current.Volume);

        Assert.Empty(Backups());
        Assert.Contains("\"$version\": 2", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Versioned_AFailingStepIsReportedAndTheFileSetAside()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath) { ThrowOnStep = 1 };
        SettingsCorruptFileEventArgs? corrupt = null;
        store.CorruptFileDetected += (_, e) => corrupt = e;

        Assert.Equal(5, store.Current.Volume);

        SettingsMigrationException failure = Assert.IsType<SettingsMigrationException>(corrupt?.Exception);
        Assert.Equal(1, failure.FromVersion);
        Assert.Equal(2, failure.ToVersion);
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.True(corrupt!.IsSetAside);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.Null(store.FileVersion);
    }

    [Theory]
    [InlineData("""{ "$version": "two" }""")]
    [InlineData("""{ "$version": -1 }""")]
    [InlineData("""[1, 2]""")]
    public void Versioned_AnInvalidVersionOrShapeIsCorrupt(string text)
    {
        File.WriteAllText(FilePath, text);
        VersionedStore store = new(FilePath) { OverrideCorruptFile = false };
        SettingsCorruptFileEventArgs? corrupt = null;
        store.CorruptFileDetected += (_, e) => corrupt = e;

        Assert.Equal(5, store.Current.Volume);

        Assert.IsAssignableFrom<JsonException>(corrupt?.Exception);
        Assert.Empty(store.Steps);
    }

    [Fact]
    public void Versioned_ExternalChangesAreUpgradedToo()
    {
        VersionedStore store = new(FilePath);
        store.Update(s => s.Volume = 10);

        File.WriteAllText(FilePath, """{ "$version": 1, "color": "Blue", "volume": 7 }""");
        store.ReloadIfChangedExternally();

        Assert.Equal(70, store.Current.Volume);
        Assert.Contains("\"$version\": 2", File.ReadAllText(FilePath));
    }

    private sealed class RenamedVersionStore(string path) : SettingsStore<VersionedSettings>(path)
    {
        public override int Version => 3;

        protected override string VersionPropertyName => "version";

        protected override void ConfigureMigrations(SettingsMigrations migrations) =>
            migrations.Add(fromVersion: 2, json => json.RenameProperty("colour", "color"));
    }

    [Fact]
    public void Versioned_CanTakeOverAnExistingVersionProperty()
    {
        File.WriteAllText(FilePath, """{ "version": 2, "colour": "Blue" }""");
        RenamedVersionStore store = new(FilePath);

        Assert.Equal("Blue", store.Current.Color);
        Assert.Equal(2, store.FileVersion);
        string text = File.ReadAllText(FilePath);
        Assert.Contains("\"version\": 3", text);
        Assert.DoesNotContain("$version", text);
    }

    // Version 5, with steps only for 1 -> 2 and 4 -> 5: 2 -> 3 and 3 -> 4 are missing.
    private sealed class FiveVersionStore(string path) : SettingsStore<VersionedSettings>(path)
    {
        public List<int> Steps { get; } = [];

        public override int Version => 5;

        public List<SettingsMigrationException> Failures { get; } = [];

        protected override void ConfigureMigrations(SettingsMigrations migrations) => migrations
            .Add(fromVersion: 1, json =>
            {
                Steps.Add(1);
                json.RenameProperty("colour", "color");
            })
            .Add(fromVersion: 4, json =>
            {
                Steps.Add(4);
                json["volume"] = json["volume"]!.GetValue<int>() + 1;
            });

        protected override SettingsMigrationFailureAction OnMigrationFailed(SettingsMigrationException exception)
        {
            Failures.Add(exception);
            return base.OnMigrationFailed(exception);
        }
    }

    [Fact]
    public void Versioned_RunsEveryStepInOrderAndSkipsMissingOnes()
    {
        File.WriteAllText(FilePath, """{ "$version": 1, "colour": "Blue", "volume": 3 }""");
        FiveVersionStore store = new(FilePath);

        Assert.Equal("Blue", store.Current.Color);
        Assert.Equal(4, store.Current.Volume);
        Assert.Equal([1, 4], store.Steps);
        Assert.Empty(store.Failures);
        Assert.Contains("\"$version\": 5", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Versioned_MissingStepAsErrorUsesTheDefaults()
    {
        const string old = """{ "$version": 1, "colour": "Blue", "volume": 3 }""";
        File.WriteAllText(FilePath, old);
        FiveVersionStore store = new(FilePath) { TreatMissingMigrationAsError = true };
        SettingsCorruptFileEventArgs? corrupt = null;
        store.CorruptFileDetected += (_, e) => corrupt = e;

        Assert.Equal(new VersionedSettings().Color, store.Current.Color);
        Assert.Equal([1], store.Steps);   // gave up at the first missing step, 2 -> 3
        SettingsMigrationException failure = Assert.Single(store.Failures);
        Assert.True(failure.IsMissingStep);
        Assert.Equal(2, failure.FromVersion);
        Assert.Equal(3, failure.ToVersion);
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Same(failure, corrupt?.Exception);
    }

    [Fact]
    public void Versioned_MissingStepAsErrorWithSkipStepCarriesOn()
    {
        File.WriteAllText(FilePath, """{ "$version": 1, "colour": "Blue", "volume": 3 }""");
        FiveVersionStore store = new(FilePath)
        {
            TreatMissingMigrationAsError = true,
            MigrationFailureAction = SettingsMigrationFailureAction.SkipStep,
        };

        Assert.Equal("Blue", store.Current.Color);
        Assert.Equal(4, store.Current.Volume);
        Assert.Equal([1, 4], store.Steps);
        Assert.Equal([2, 3], store.Failures.Select(f => f.FromVersion));
        Assert.All(store.Failures, f => Assert.True(f.IsMissingStep));
        Assert.Contains("\"$version\": 5", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Versioned_MissingStepAsErrorWithThrowLeavesTheFileAlone()
    {
        const string old = """{ "$version": 1, "colour": "Blue", "volume": 3 }""";
        File.WriteAllText(FilePath, old);
        FiveVersionStore store = new(FilePath)
        {
            TreatMissingMigrationAsError = true,
            MigrationFailureAction = SettingsMigrationFailureAction.Throw,
        };

        SettingsMigrationException failure = Assert.Throws<SettingsMigrationException>(() => store.Current);

        Assert.True(failure.IsMissingStep);
        Assert.Equal(old, File.ReadAllText(FilePath));
    }

    private sealed class ConfiguredStore(string path, int version, Action<SettingsMigrations> configure)
        : SettingsStore<VersionedSettings>(path)
    {
        public int Configured { get; private set; }

        public SettingsMigrations? Migrations { get; private set; }

        public override int Version => version;

        protected override void ConfigureMigrations(SettingsMigrations migrations)
        {
            Configured++;
            Migrations = migrations;
            configure(migrations);
        }
    }

    [Fact]
    public void Migrations_AreConfiguredOnceOnFirstRead()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue" }""");
        ConfiguredStore store = new(FilePath, 1, m => m.Add(0, json => json.RenameProperty("colour", "color")));

        Assert.Equal(0, store.Configured);   // not from the constructor
        Assert.Equal("Blue", store.Current.Color);
        store.Load();

        Assert.Equal(1, store.Configured);
        Assert.Equal(1, store.Migrations?.Count);
        Assert.True(store.Migrations?.Contains(0));
        Assert.Equal([0], store.Migrations?.FromVersions);
    }

    [Fact]
    public void Migrations_CantChangeAfterConfiguring()
    {
        File.WriteAllText(FilePath, "{}");
        ConfiguredStore store = new(FilePath, 2, _ => { });
        _ = store.Current;

        Assert.Throws<InvalidOperationException>(() => store.Migrations!.Add(0, _ => { }));
    }

    [Fact]
    public void Migrations_RejectDuplicateAndNegativeVersions()
    {
        File.WriteAllText(FilePath, "{}");

        ConfiguredStore duplicate = new(FilePath, 2, m => m.Add(0, _ => { }).Add(0, _ => { }));
        ConfiguredStore negative = new(FilePath, 2, m => m.Add(-1, _ => { }));

        Assert.Throws<ArgumentException>(() => duplicate.Current);
        Assert.Throws<ArgumentOutOfRangeException>(() => negative.Current);
    }

    [Theory]
    [InlineData(0)]   // steps but no Version
    [InlineData(2)]   // a step from 2 can't run when Version is 2
    public void Migrations_StepAtOrAboveVersionThrows(int version)
    {
        const string old = """{ "colour": "Blue" }""";
        File.WriteAllText(FilePath, old);
        ConfiguredStore store = new(FilePath, version, m => m.Add(0, _ => { }).Add(2, _ => { }));

        Assert.Throws<InvalidOperationException>(() => store.Current);
        Assert.Equal(old, File.ReadAllText(FilePath));
    }

    [Fact]
    public void Versioned_StepThatThrowsIsNotAMissingStep()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath) { ThrowOnStep = 0, TreatMissingMigrationAsError = true };

        _ = store.Current;

        Assert.False(Assert.Single(store.Failures).IsMissingStep);
    }

    [Fact]
    public void Versioned_FailureHookSeesEachFailure()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath) { ThrowOnStep = 0 };

        _ = store.Current;

        SettingsMigrationException failure = Assert.Single(store.Failures);
        Assert.Equal(0, failure.FromVersion);
        Assert.Equal(1, failure.ToVersion);
        Assert.Equal("boom", failure.InnerException?.Message);
    }

    [Fact]
    public void Versioned_SkipStepUndoesTheStepAndCarriesOn()
    {
        File.WriteAllText(FilePath, """{ "$version": 1, "color": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath)
        {
            ThrowOnStep = 1,
            MigrationFailureAction = SettingsMigrationFailureAction.SkipStep,
        };
        bool corrupt = false;
        store.CorruptFileDetected += (_, _) => corrupt = true;

        Assert.Equal("Blue", store.Current.Color);   // the step's partial change was undone
        Assert.Equal(3, store.Current.Volume);       // and its real change never happened
        Assert.Equal(1, store.UpgradedFrom);
        Assert.False(corrupt);
        Assert.Contains("\"$version\": 2", File.ReadAllText(FilePath));
        Assert.Single(Backups());
    }

    [Fact]
    public void Versioned_SkipStepOnOnUpgradedDeserializesAgain()
    {
        File.WriteAllText(FilePath, """{ "colour": "Blue", "volume": 3 }""");
        VersionedStore store = new(FilePath)
        {
            ThrowOnUpgraded = true,
            MigrationFailureAction = SettingsMigrationFailureAction.SkipStep,
        };

        Assert.Equal("Blue", store.Current.Color);
        Assert.Equal(30, store.Current.Volume);
        Assert.Equal(0, store.Current.Loudness);
        SettingsMigrationException failure = Assert.Single(store.Failures);
        Assert.Equal(0, failure.FromVersion);
        Assert.Equal(2, failure.ToVersion);
    }

    [Fact]
    public void Versioned_ThrowLeavesTheFileAloneAndTriesAgainNextTime()
    {
        const string old = """{ "colour": "Blue", "volume": 3 }""";
        File.WriteAllText(FilePath, old);
        VersionedStore store = new(FilePath)
        {
            ThrowOnStep = 1,
            MigrationFailureAction = SettingsMigrationFailureAction.Throw,
        };
        bool corrupt = false;
        store.CorruptFileDetected += (_, _) => corrupt = true;

        SettingsMigrationException failure = Assert.Throws<SettingsMigrationException>(() => store.Current);
        Assert.Equal(1, failure.FromVersion);
        Assert.Throws<SettingsMigrationException>(() => store.Update(s => s.Volume = 1));

        Assert.Equal(2, store.Failures.Count);   // read again, not cached
        Assert.False(corrupt);
        Assert.Equal(old, File.ReadAllText(FilePath));
        Assert.False(File.Exists(FilePath + ".bad"));
        Assert.Empty(Backups());
    }

    [Fact]
    public void Versioned_ThrowWhileWatchingKeepsTheCurrentSettings()
    {
        VersionedStore store = new(FilePath)
        {
            ThrowOnStep = 1,
            MigrationFailureAction = SettingsMigrationFailureAction.Throw,
        };
        store.Update(s => s.Volume = 10);
        int loads = 0;
        store.Changed += (_, e) => loads += e.Source == SettingsChangeSource.Load ? 1 : 0;

        File.WriteAllText(FilePath, """{ "$version": 1, "color": "Blue", "volume": 7 }""");
        store.ReloadIfChangedExternally();

        Assert.Equal(10, store.Current.Volume);
        Assert.Equal(0, loads);
        Assert.Null(store.FileVersion);
    }
}
