using System.Text.Json;
using Niddy.Core.Serialization;

namespace Niddy.Core.Tests;

public sealed class SerializationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "niddy-json-" + Guid.NewGuid().ToString("N"));

    public enum Color { Red, Green }

    public sealed class Sample
    {
        public string Name { get; set; } = "";
        public Color Color { get; set; }
        public int Count;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void NiddyOptions_AreOneReadOnlyInstance()
    {
        Assert.Same(JsonSerializerOptions.Niddy, JsonSerializerOptions.Niddy);
        Assert.True(JsonSerializerOptions.Niddy.IsReadOnly);

        string json = JsonSerializer.Serialize(new Sample { Name = "a", Color = Color.Green, Count = 2 }, JsonSerializerOptions.Niddy);

        Assert.Contains("\"name\": \"a\"", json);
        Assert.Contains("\"color\": \"Green\"", json);
        Assert.Contains("\"count\": 2", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    public void DeserializeOrDefault_ReturnsDefaultForBadInput(string? json)
    {
        Assert.Null(JsonSerializer.DeserializeOrDefault<Sample>(json));
    }

    [Fact]
    public void DeserializeOrDefault_UsesNiddyOptionsByDefault()
    {
        Sample? sample = JsonSerializer.DeserializeOrDefault<Sample>("{ \"name\": \"b\", \"color\": \"Green\", \"count\": 3 }");

        Assert.NotNull(sample);
        Assert.Equal("b", sample.Name);
        Assert.Equal(Color.Green, sample.Color);
        Assert.Equal(3, sample.Count);
    }

    [Fact]
    public void JsonFile_RoundTrips()
    {
        string path = Path.Combine(_dir, "nested", "sample.json");

        JsonFile.Write(path, new Sample { Name = "c", Color = Color.Green, Count = 4 });
        Sample? sample = JsonFile.Read<Sample>(path);

        Assert.NotNull(sample);
        Assert.Equal("c", sample.Name);
        Assert.Equal(Color.Green, sample.Color);
        Assert.Equal(4, sample.Count);
    }

    [Fact]
    public async Task JsonFile_RoundTripsAsync()
    {
        string path = Path.Combine(_dir, "sample.json");
        CancellationToken ct = TestContext.Current.CancellationToken;

        await JsonFile.WriteAsync(path, new Sample { Name = "d" }, cancellationToken: ct);
        Sample? sample = await JsonFile.ReadAsync<Sample>(path, cancellationToken: ct);

        Assert.Equal("d", sample?.Name);
    }

    [Fact]
    public void JsonFile_ReadReturnsDefaultForMissingOrEmptyFile()
    {
        string path = Path.Combine(_dir, "empty.json");

        Assert.Null(JsonFile.Read<Sample>(path));

        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "");

        Assert.Null(JsonFile.Read<Sample>(path));
    }
}
